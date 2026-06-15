using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Users;
using FriendCore.IntegrationTests.Infrastructure;

namespace FriendCore.IntegrationTests.Moderation;

/// <summary>
///     Spec §13.5 metrics dashboard. This class gets its own fresh database (one container per
///     test class), and only the comprehensive test creates reports/appeals/actions, so the
///     absolute counts are deterministic.
/// </summary>
public class ModerationMetricsFlowTests(FriendCoreApiFactory factory) : IClassFixture<FriendCoreApiFactory>
{
    private readonly AuthScenarios _scenarios = new(factory);

    private async Task<(HttpClient Client, string Username)> RegisterUser()
    {
        return await _scenarios.RegisterVerifiedClientAsync();
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

    [Fact]
    public async Task Metrics_ReflectReportsAppealsAndActions()
    {
        // Two reported posts.
        var (authorA, authorAName) = await RegisterUser();
        var (authorB, _) = await RegisterUser();
        var postA = await CreatePost(authorA, "post a");
        var postB = await CreatePost(authorB, "post b");
        var (reporter, _) = await RegisterUser();
        await reporter.PostAsJsonAsync($"/api/v1/posts/{postA}/report", new { category = "Spam" });
        await reporter.PostAsJsonAsync($"/api/v1/posts/{postB}/report", new { category = "Spam" });

        var moderator = await ModeratorClient();
        var queue = await moderator.GetFromJsonAsync<ReportQueueResponse>("/api/v1/admin/reports");
        var reportA = queue!.Reports.First(r => r.TargetId == postA).Id;
        var reportB = queue.Reports.First(r => r.TargetId == postB).Id;

        // Resolve one (content removal), dismiss the other → 2 moderator actions.
        await moderator.PostAsJsonAsync(
            $"/api/v1/admin/reports/{reportA}/resolve", new { action = "ContentRemoval", reason = "spam" });
        await moderator.PostAsJsonAsync(
            $"/api/v1/admin/reports/{reportB}/resolve", new { action = "Dismiss", reason = "looked fine" });

        // Ban authorA directly → a 3rd moderator action; authorA appeals; a 2nd moderator overturns.
        await moderator.PostAsJsonAsync(
            $"/api/v1/admin/users/{authorAName}/action", new { action = "PermanentBan", reason = "repeat" });
        var actions = await authorA.GetFromJsonAsync<MyModerationActionsResponse>("/api/v1/users/me/moderation-actions");
        await authorA.PostAsJsonAsync(
            "/api/v1/appeals", new { auditLogId = actions!.Actions[0].Id, reason = "unfair" });
        var moderator2 = await ModeratorClient();
        var appeals = await moderator2.GetFromJsonAsync<AppealQueueResponse>("/api/v1/admin/appeals");
        await moderator2.PostAsJsonAsync(
            $"/api/v1/admin/appeals/{appeals!.Appeals[0].Id}/review", new { outcome = "Overturned", note = "ok" });

        var metrics = await moderator.GetFromJsonAsync<ModerationMetricsResponse>("/api/v1/admin/metrics");

        Assert.Equal(2, metrics!.Reports.Total);
        Assert.Equal(0, metrics.Reports.Pending);
        Assert.Equal(1, metrics.Reports.Resolved);
        Assert.Equal(1, metrics.Reports.Dismissed);
        Assert.NotNull(metrics.Reports.AvgResolutionHours);

        Assert.Equal(1, metrics.Appeals.Total);
        Assert.Equal(0, metrics.Appeals.Pending);
        Assert.Equal(1, metrics.Appeals.Reviewed);
        Assert.Equal(1, metrics.Appeals.Overturned);
        Assert.Equal(1.0, metrics.Appeals.OverturnRate);

        // Content removal + dismissal + ban = 3 logged actions (§10.3 every action logged).
        Assert.Equal(3, metrics.TotalModerationActions);
    }

    [Fact]
    public async Task Metrics_ForbiddenForRegularUser()
    {
        var (user, _) = await RegisterUser();

        var response = await user.GetAsync("/api/v1/admin/metrics");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Metrics_RequireAuth()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/admin/metrics");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
