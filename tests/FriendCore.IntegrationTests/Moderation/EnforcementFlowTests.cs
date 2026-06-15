using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Users;
using FriendCore.IntegrationTests.Infrastructure;

namespace FriendCore.IntegrationTests.Moderation;

/// <summary>
///     Spec §13.2/§13.5/§10.3: a moderator resolves reports or acts on accounts; the runtime
///     enforcement (content removal / suspension / ban) takes effect, and every action is logged
///     to the audit trail with its mandatory reason.
/// </summary>
public class EnforcementFlowTests(FriendCoreApiFactory factory) : IClassFixture<FriendCoreApiFactory>
{
    private readonly AuthScenarios _scenarios = new(factory);

    private async Task<(HttpClient Client, string Username, string Email)> RegisterUser()
    {
        factory.Clock.Reset();
        var client = factory.CreateClient();
        var email = AuthScenarios.UniqueEmail();
        var username = AuthScenarios.NewUsername();
        await AuthScenarios.Register(client, email, username);
        await _scenarios.MarkVerifiedAsync(email);
        var login = await AuthScenarios.Login(client, email);
        var body = await login.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        return (client, username, email);
    }

    private async Task<HttpClient> ModeratorClient()
    {
        factory.Clock.Reset();
        var client = factory.CreateClient();
        var token = await _scenarios.RegisterWithRoleAndGetToken(
            client, AuthScenarios.UniqueEmail(), UserRole.Moderator);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<string> CreatePost(HttpClient client, string content)
    {
        var response = await client.PostAsJsonAsync("/api/v1/posts", new { content, sensitivity = "General" });
        var body = await response.Content.ReadFromJsonAsync<PostResponse>();
        return body!.Id;
    }

    private static async Task<string> ReportAndGetId(HttpClient reporter, HttpClient moderator, string postId)
    {
        await reporter.PostAsJsonAsync($"/api/v1/posts/{postId}/report", new { category = "Spam" });
        var queue = await moderator.GetFromJsonAsync<ReportQueueResponse>("/api/v1/admin/reports");
        return queue!.Reports.First(r => r.TargetId == postId).Id;
    }

    [Fact]
    public async Task ResolveWithContentRemoval_DeletesPost_AndLogsAction()
    {
        var (author, _, _) = await RegisterUser();
        var (reporter, _) = await _scenarios.RegisterVerifiedClientAsync();
        var postId = await CreatePost(author, "remove me");
        var moderator = await ModeratorClient();
        var reportId = await ReportAndGetId(reporter, moderator, postId);

        var resolve = await moderator.PostAsJsonAsync(
            $"/api/v1/admin/reports/{reportId}/resolve",
            new { action = "ContentRemoval", reason = "violates rule 1" });
        Assert.Equal(HttpStatusCode.NoContent, resolve.StatusCode);

        // Post is soft-deleted — gone for everyone.
        var fetch = await author.GetAsync($"/api/v1/posts/{postId}");
        Assert.Equal(HttpStatusCode.NotFound, fetch.StatusCode);

        var log = await moderator.GetFromJsonAsync<AuditLogResponse>("/api/v1/admin/audit-log");
        Assert.Contains(log!.Entries, e => e.Action == "ContentRemoval" && e.Reason == "violates rule 1");
    }

    [Fact]
    public async Task ResolveWithBan_BansAuthor_WhoCanNoLongerLogIn()
    {
        var (author, _, authorEmail) = await RegisterUser();
        var (reporter, _) = await _scenarios.RegisterVerifiedClientAsync();
        var postId = await CreatePost(author, "ban-worthy");
        var moderator = await ModeratorClient();
        var reportId = await ReportAndGetId(reporter, moderator, postId);

        var resolve = await moderator.PostAsJsonAsync(
            $"/api/v1/admin/reports/{reportId}/resolve",
            new { action = "PermanentBan", reason = "repeated severe violations" });
        Assert.Equal(HttpStatusCode.NoContent, resolve.StatusCode);

        var login = await AuthScenarios.Login(factory.CreateClient(), authorEmail);
        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
    }

    [Fact]
    public async Task DirectSuspension_BlocksLogin_AndShowsOnAccountView()
    {
        var (_, targetName, targetEmail) = await RegisterUser();
        var moderator = await ModeratorClient();

        var act = await moderator.PostAsJsonAsync(
            $"/api/v1/admin/users/{targetName}/action",
            new { action = "TemporarySuspension", reason = "cooling off", suspensionDays = 7 });
        Assert.Equal(HttpStatusCode.NoContent, act.StatusCode);

        var login = await AuthScenarios.Login(factory.CreateClient(), targetEmail);
        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);

        var account = await moderator.GetFromJsonAsync<UserAccountResponse>($"/api/v1/admin/users/{targetName}");
        Assert.NotNull(account!.SuspendedUntil);
        Assert.Contains(account.ModerationHistory, e => e.Action == "TemporarySuspension" && e.Reason == "cooling off");
    }

    [Fact]
    public async Task ResolveWithDismiss_ClosesReport_WithNoEnforcement()
    {
        var (author, _, authorEmail) = await RegisterUser();
        var (reporter, _) = await _scenarios.RegisterVerifiedClientAsync();
        var postId = await CreatePost(author, "actually fine");
        var moderator = await ModeratorClient();
        var reportId = await ReportAndGetId(reporter, moderator, postId);

        var dismiss = await moderator.PostAsJsonAsync(
            $"/api/v1/admin/reports/{reportId}/resolve",
            new { action = "Dismiss", reason = "no violation found" });
        Assert.Equal(HttpStatusCode.NoContent, dismiss.StatusCode);

        // Post survives, author can still log in.
        var fetch = await author.GetAsync($"/api/v1/posts/{postId}");
        Assert.Equal(HttpStatusCode.OK, fetch.StatusCode);
        var login = await AuthScenarios.Login(factory.CreateClient(), authorEmail);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        // Re-resolving the now-closed report conflicts.
        var again = await moderator.PostAsJsonAsync(
            $"/api/v1/admin/reports/{reportId}/resolve",
            new { action = "ContentRemoval", reason = "changed my mind" });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Resolve_WithoutReason_ReturnsBadRequest()
    {
        var (author, _) = await _scenarios.RegisterVerifiedClientAsync();
        var (reporter, _) = await _scenarios.RegisterVerifiedClientAsync();
        var postId = await CreatePost(author, "needs reason");
        var moderator = await ModeratorClient();
        var reportId = await ReportAndGetId(reporter, moderator, postId);

        var resolve = await moderator.PostAsJsonAsync(
            $"/api/v1/admin/reports/{reportId}/resolve", new { action = "ContentRemoval", reason = "" });

        Assert.Equal(HttpStatusCode.BadRequest, resolve.StatusCode);
        Assert.Contains("Moderation.ReasonRequired", await resolve.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ActOnUser_ContentRemoval_ReturnsBadRequest()
    {
        var (_, targetName, _) = await RegisterUser();
        var moderator = await ModeratorClient();

        var act = await moderator.PostAsJsonAsync(
            $"/api/v1/admin/users/{targetName}/action",
            new { action = "ContentRemoval", reason = "wrong action type" });

        Assert.Equal(HttpStatusCode.BadRequest, act.StatusCode);
    }

    [Fact]
    public async Task AdminEndpoints_ForbiddenForRegularUser()
    {
        var (user, _) = await _scenarios.RegisterVerifiedClientAsync();

        Assert.Equal(HttpStatusCode.Forbidden,
            (await user.GetAsync("/api/v1/admin/audit-log")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await user.PostAsJsonAsync($"/api/v1/admin/users/{AuthScenarios.NewUsername()}/action",
                new { action = "PermanentBan", reason = "nope" })).StatusCode);
    }

    [Fact]
    public async Task ModerationAction_RecordsRequestIpInAuditLog()
    {
        // §10.3: the audit entry captures the moderator's request IP. Verifies the real
        // HttpCurrentUserService.IpAddress wiring flows through to the persisted log.
        var (_, targetName, _) = await RegisterUser();
        var moderator = await ModeratorClient();
        await moderator.PostAsJsonAsync(
            $"/api/v1/admin/users/{targetName}/action", new { action = "Warning", reason = "ip-capture check" });

        var log = await moderator.GetFromJsonAsync<AuditLogResponse>("/api/v1/admin/audit-log");
        var entry = log!.Entries.First(e => e.Reason == "ip-capture check");
        Assert.False(string.IsNullOrWhiteSpace(entry.IpAddress));
    }

    [Fact]
    public async Task AdminEndpoints_RequireAuth()
    {
        var anon = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/v1/admin/audit-log")).StatusCode);
    }
}
