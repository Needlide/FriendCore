using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Users;
using FriendCore.IntegrationTests.Infrastructure;

namespace FriendCore.IntegrationTests.Moderation;

/// <summary>
///     Spec §13.3 appeals: a user appeals an action taken against them; a DIFFERENT moderator
///     reviews it; Overturned reverses the enforcement. The original moderator cannot review.
/// </summary>
public class AppealFlowTests(FriendCoreApiFactory factory) : IClassFixture<FriendCoreApiFactory>
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
        var login = await _scenarios.LoginAsync(client, email);
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

    private static async Task Ban(HttpClient moderator, string username, string reason)
    {
        var response = await moderator.PostAsJsonAsync(
            $"/api/v1/admin/users/{username}/action", new { action = "PermanentBan", reason });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static async Task<string> MyFirstActionId(HttpClient user)
    {
        var actions = await user.GetFromJsonAsync<MyModerationActionsResponse>(
            "/api/v1/users/me/moderation-actions");
        return actions!.Actions[0].Id;
    }

    [Fact]
    public async Task Appeal_OverturnedByDifferentModerator_LiftsTheBan()
    {
        var (offender, offenderName, offenderEmail) = await RegisterUser();
        var moderatorA = await ModeratorClient();
        await Ban(moderatorA, offenderName, "spam ring");

        // The offender still holds a pre-ban token, sees the action, and appeals it.
        var actionId = await MyFirstActionId(offender);
        var submit = await offender.PostAsJsonAsync(
            "/api/v1/appeals", new { auditLogId = actionId, reason = "I was hacked, not me" });
        Assert.Equal(HttpStatusCode.NoContent, submit.StatusCode);

        // A second moderator reviews from the queue and overturns.
        var moderatorB = await ModeratorClient();
        var queue = await moderatorB.GetFromJsonAsync<AppealQueueResponse>("/api/v1/admin/appeals");
        var appeal = queue!.Appeals.Single(a => a.Appellant.Username == offenderName);

        var review = await moderatorB.PostAsJsonAsync(
            $"/api/v1/admin/appeals/{appeal.Id}/review", new { outcome = "Overturned", note = "wrongly flagged" });
        Assert.Equal(HttpStatusCode.NoContent, review.StatusCode);

        // Ban lifted — the offender can log in again.
        var login = await _scenarios.LoginAsync(factory.CreateClient(), offenderEmail);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task OriginalModerator_CannotReviewTheAppeal()
    {
        var (offender, offenderName, _) = await RegisterUser();
        var moderatorA = await ModeratorClient();
        await Ban(moderatorA, offenderName, "abuse");
        var actionId = await MyFirstActionId(offender);
        await offender.PostAsJsonAsync("/api/v1/appeals", new { auditLogId = actionId, reason = "please" });

        // Moderator A (who issued the ban) finds the appeal in the queue but cannot review it.
        var queue = await moderatorA.GetFromJsonAsync<AppealQueueResponse>("/api/v1/admin/appeals");
        var appeal = queue!.Appeals.Single(a => a.Appellant.Username == offenderName);

        var review = await moderatorA.PostAsJsonAsync(
            $"/api/v1/admin/appeals/{appeal.Id}/review", new { outcome = "Upheld", note = "stands" });

        // Forbidden returns an empty body by convention (anti-enumeration), so assert on status.
        Assert.Equal(HttpStatusCode.Forbidden, review.StatusCode);
    }

    [Fact]
    public async Task Appeal_OverturnedContentRemoval_RestoresThePost()
    {
        var (author, authorName, _) = await RegisterUser();
        var postResponse = await author.PostAsJsonAsync(
            "/api/v1/posts", new { content = "restore me", sensitivity = "General" });
        var post = await postResponse.Content.ReadFromJsonAsync<PostResponse>();
        var (reporter, _) = await _scenarios.RegisterVerifiedClientAsync();
        await reporter.PostAsJsonAsync($"/api/v1/posts/{post!.Id}/report", new { category = "Spam" });

        var moderatorA = await ModeratorClient();
        var reportQueue = await moderatorA.GetFromJsonAsync<ReportQueueResponse>("/api/v1/admin/reports");
        var reportId = reportQueue!.Reports.First(r => r.TargetId == post.Id).Id;
        await moderatorA.PostAsJsonAsync(
            $"/api/v1/admin/reports/{reportId}/resolve", new { action = "ContentRemoval", reason = "removed" });
        Assert.Equal(HttpStatusCode.NotFound, (await author.GetAsync($"/api/v1/posts/{post.Id}")).StatusCode);

        // The author appeals the removal (audit id obtained from the admin log in this test;
        // user-side discovery of content-removal actions is a later addition).
        var log = await moderatorA.GetFromJsonAsync<AuditLogResponse>("/api/v1/admin/audit-log");
        var removalId = log!.Entries.First(e => e.Action == "ContentRemoval").Id;
        await author.PostAsJsonAsync("/api/v1/appeals", new { auditLogId = removalId, reason = "it was fine" });

        var moderatorB = await ModeratorClient();
        var queue = await moderatorB.GetFromJsonAsync<AppealQueueResponse>("/api/v1/admin/appeals");
        var appeal = queue!.Appeals.Single(a => a.AuditLogId == removalId);
        await moderatorB.PostAsJsonAsync(
            $"/api/v1/admin/appeals/{appeal.Id}/review",
            new { outcome = "Overturned", note = "reinstated" });

        // Post is back.
        Assert.Equal(HttpStatusCode.OK, (await author.GetAsync($"/api/v1/posts/{post.Id}")).StatusCode);
    }

    [Fact]
    public async Task Appeal_ForActionNotAgainstYou_ReturnsNotFound()
    {
        var (offender, offenderName, _) = await RegisterUser();
        var moderator = await ModeratorClient();
        await Ban(moderator, offenderName, "abuse");
        var actionId = await MyFirstActionId(offender);

        // A different user tries to appeal someone else's ban.
        var (other, _) = await _scenarios.RegisterVerifiedClientAsync();
        var response = await other.PostAsJsonAsync(
            "/api/v1/appeals", new { auditLogId = actionId, reason = "not mine to appeal" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Appeal_Twice_ReturnsConflict()
    {
        var (offender, offenderName, _) = await RegisterUser();
        var moderator = await ModeratorClient();
        await Ban(moderator, offenderName, "abuse");
        var actionId = await MyFirstActionId(offender);

        await offender.PostAsJsonAsync("/api/v1/appeals", new { auditLogId = actionId, reason = "first" });
        var second = await offender.PostAsJsonAsync(
            "/api/v1/appeals", new { auditLogId = actionId, reason = "second" });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task ReviewAppeal_WithoutNote_ReturnsBadRequest()
    {
        var (offender, offenderName, _) = await RegisterUser();
        var moderatorA = await ModeratorClient();
        await Ban(moderatorA, offenderName, "abuse");
        var actionId = await MyFirstActionId(offender);
        await offender.PostAsJsonAsync("/api/v1/appeals", new { auditLogId = actionId, reason = "please" });

        var moderatorB = await ModeratorClient();
        var queue = await moderatorB.GetFromJsonAsync<AppealQueueResponse>("/api/v1/admin/appeals");
        var appeal = queue!.Appeals.Single(a => a.Appellant.Username == offenderName);
        var review = await moderatorB.PostAsJsonAsync(
            $"/api/v1/admin/appeals/{appeal.Id}/review", new { outcome = "Upheld", note = "" });

        Assert.Equal(HttpStatusCode.BadRequest, review.StatusCode);
    }

    [Fact]
    public async Task AppealQueue_ForbiddenForRegularUser()
    {
        var (user, _) = await _scenarios.RegisterVerifiedClientAsync();

        var response = await user.GetAsync("/api/v1/admin/appeals");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SubmitAppeal_RequiresAuth()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/v1/appeals", new { auditLogId = Ulid.NewUlid().ToString(), reason = "x" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
