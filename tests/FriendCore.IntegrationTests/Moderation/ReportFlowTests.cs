using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FriendCore.Application.Common.Models;
using FriendCore.Application.Media.Queries;
using FriendCore.Domain.Users;
using FriendCore.IntegrationTests.Infrastructure;
using FriendCore.IntegrationTests.Media;

namespace FriendCore.IntegrationTests.Moderation;

/// <summary>
///     Spec §13.4/§13.5 + §17.2: users report posts and accounts; moderators see a
///     priority-sorted queue under /api/v1/admin. Regular users can't reach the queue.
/// </summary>
public class ReportFlowTests(FriendCoreApiFactory factory) : IClassFixture<FriendCoreApiFactory>
{
    private readonly AuthScenarios _scenarios = new(factory);

    private Task<(HttpClient Client, string Username)> RegisterClient() =>
        _scenarios.RegisterVerifiedClientAsync();

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
    public async Task ReportPost_ThenAppearsInModeratorQueue()
    {
        var (author, _) = await RegisterClient();
        var (reporter, reporterName) = await RegisterClient();
        var postId = await CreatePost(author, "questionable");

        var report = await reporter.PostAsJsonAsync(
            $"/api/v1/posts/{postId}/report", new { category = "Spam", note = "looks like spam" });
        Assert.Equal(HttpStatusCode.NoContent, report.StatusCode);

        var moderator = await ModeratorClient();
        var queue = await moderator.GetFromJsonAsync<ReportQueueResponse>("/api/v1/admin/reports");

        var entry = Assert.Single(queue!.Reports, r => r.TargetId == postId);
        Assert.Equal("Post", entry.TargetType);
        Assert.Equal("Spam", entry.Category);
        Assert.Equal("Low", entry.Priority);
        Assert.Equal("Pending", entry.Status);
        Assert.Equal(reporterName, entry.Reporter.Username);
    }

    [Fact]
    public async Task ReportUser_AppearsInQueue_AsUserTarget()
    {
        var (_, targetName) = await RegisterClient();
        var (reporter, _) = await RegisterClient();

        var report = await reporter.PostAsJsonAsync(
            $"/api/v1/users/{targetName}/report", new { category = "Harassment" });
        Assert.Equal(HttpStatusCode.NoContent, report.StatusCode);

        var moderator = await ModeratorClient();
        var queue = await moderator.GetFromJsonAsync<ReportQueueResponse>("/api/v1/admin/reports");

        Assert.Contains(queue!.Reports, r => r.TargetType == "User" && r.Category == "Harassment");
    }

    [Fact]
    public async Task Queue_IsPrioritySorted_HighestFirst()
    {
        var (author, _) = await RegisterClient();
        var lowPost = await CreatePost(author, "low");
        var highPost = await CreatePost(author, "high");
        var (reporterA, _) = await RegisterClient();
        var (reporterB, _) = await RegisterClient();

        await reporterA.PostAsJsonAsync($"/api/v1/posts/{lowPost}/report", new { category = "Spam" });
        await reporterB.PostAsJsonAsync($"/api/v1/posts/{highPost}/report", new { category = "InvolvesMinor" });

        var moderator = await ModeratorClient();
        var reports = (await moderator.GetFromJsonAsync<ReportQueueResponse>("/api/v1/admin/reports"))!.Reports;

        var highIndex = reports.ToList().FindIndex(r => r.TargetId == highPost);
        var lowIndex = reports.ToList().FindIndex(r => r.TargetId == lowPost);
        Assert.True(highIndex >= 0 && lowIndex >= 0);
        Assert.True(highIndex < lowIndex, "High-priority report should sort before low-priority.");
    }

    [Fact]
    public async Task ReportPost_IsIdempotent_PerReporter()
    {
        var (author, _) = await RegisterClient();
        var (reporter, _) = await RegisterClient();
        var postId = await CreatePost(author, "dupe");

        await reporter.PostAsJsonAsync($"/api/v1/posts/{postId}/report", new { category = "Spam" });
        await reporter.PostAsJsonAsync($"/api/v1/posts/{postId}/report", new { category = "Spam" });

        var moderator = await ModeratorClient();
        var reports = (await moderator.GetFromJsonAsync<ReportQueueResponse>("/api/v1/admin/reports"))!.Reports;

        Assert.Single(reports, r => r.TargetId == postId);
    }

    [Fact]
    public async Task ReportUser_Self_ReturnsBadRequest()
    {
        var (client, username) = await RegisterClient();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/users/{username}/report", new { category = "Spam" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ReportPost_UnknownPost_ReturnsNotFound()
    {
        var (reporter, _) = await RegisterClient();

        var response = await reporter.PostAsJsonAsync(
            $"/api/v1/posts/{Ulid.NewUlid()}/report", new { category = "Spam" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Queue_ForbiddenForRegularUser()
    {
        var (client, _) = await RegisterClient();

        var response = await client.GetAsync("/api/v1/admin/reports");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Queue_RequiresAuth()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/admin/reports");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ReportMedia_AppearsInQueue_AsMediaTarget()
    {
        var (author, _) = await RegisterClient();
        var (reporter, _) = await RegisterClient();
        var mediaId = await UploadReadyMedia(author);

        var report = await reporter.PostAsJsonAsync(
            $"/api/v1/media/{mediaId}/report", new { category = "FlashingLights", note = "strobes" });
        Assert.Equal(HttpStatusCode.NoContent, report.StatusCode);

        var moderator = await ModeratorClient();
        var queue = await moderator.GetFromJsonAsync<ReportQueueResponse>("/api/v1/admin/reports");

        // The half that was missing entirely: the old report-safety endpoint gated the media but
        // created no Report, so this row simply never existed.
        var entry = Assert.Single(queue!.Reports, r => r.TargetId == mediaId);
        Assert.Equal("Media", entry.TargetType);
        Assert.Equal("FlashingLights", entry.Category);
        Assert.Equal("Pending", entry.Status);
    }

    [Fact]
    public async Task Moderator_CanRemoveReportedMedia_AndTheReportResolves()
    {
        var (author, _) = await RegisterClient();
        var (reporter, _) = await RegisterClient();
        var mediaId = await UploadReadyMedia(author);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await reporter.PostAsJsonAsync(
                $"/api/v1/media/{mediaId}/report", new { category = "FlashingLights" })).StatusCode);

        var moderator = await ModeratorClient();
        var queue = await moderator.GetFromJsonAsync<ReportQueueResponse>("/api/v1/admin/reports");
        var entry = Assert.Single(queue!.Reports, r => r.TargetId == mediaId);

        // ContentRemoval needed a Media branch, or this 400s and the report sits Pending forever.
        var resolved = await moderator.PostAsJsonAsync(
            $"/api/v1/admin/reports/{entry.Id}/resolve",
            new { action = "ContentRemoval", reason = "harmful image" });
        Assert.Equal(HttpStatusCode.NoContent, resolved.StatusCode);

        // Gone from the API entirely.
        var status = await author.GetAsync($"/api/v1/media/{mediaId}/status");
        Assert.Equal(HttpStatusCode.NotFound, status.StatusCode);
    }

    private static async Task<string> UploadReadyMedia(HttpClient client)
    {
        var file = new ByteArrayContent(TestImages.RealPng(60, 40));
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        var form = new MultipartFormDataContent { { file, "file", "image.png" } };

        var response = await client.PostAsync("/api/v1/media", form);
        var body = await response.Content.ReadFromJsonAsync<MediaStatusResponse>();
        Assert.Equal("Ready", await MediaUploadFlowTests.PollStatusAsync(client, body!.Id));
        return body.Id;
    }
}
