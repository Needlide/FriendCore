using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Moderation;
using FriendCore.Domain.Users;
using FriendCore.Infrastructure.Persistence;
using FriendCore.IntegrationTests.Infrastructure;
using FriendCore.IntegrationTests.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FriendCore.IntegrationTests.Moderation;

/// <summary>
///     The subject snapshot: a copy of what a report was about, taken when it was filed.
/// </summary>
/// <remarks>
///     The cases that matter are all about the subject DISAPPEARING — that is the entire reason
///     the snapshot exists, so a report whose target has been deleted is still reviewable.
/// </remarks>
public class ReportSubjectSnapshotFlowTests(FriendCoreApiFactory factory)
    : IClassFixture<FriendCoreApiFactory>
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

    private static async Task ReportSubject(HttpClient client, string path, object body)
    {
        var response = await client.PostAsJsonAsync(path, body);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static async Task<List<ReportResponse>> Queue(HttpClient moderator)
    {
        var queue = await moderator.GetFromJsonAsync<ReportQueueResponse>("/api/v1/admin/reports");
        return queue!.Reports.ToList();
    }

    /// <summary>
    ///     Reads a report straight from the database.
    /// </summary>
    /// <remarks>
    ///     Needed after RESOLUTION: the queue only carries pending reports, so once a report is
    ///     resolved there is no longer any API that will show it — which is precisely when the
    ///     purge-on-resolve behaviour has to be checked.
    /// </remarks>
    private async Task<Report> StoredReportAsync(string id)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Reports.SingleAsync(report => report.Id == Ulid.Parse(id));
    }

    /// <summary>
    ///     This test's own report out of the queue. The queue is shared across the whole class — and
    ///     across the container — so Assert.Single would be asserting that no other test ever
    ///     reported anything.
    /// </summary>
    private static async Task<ReportResponse> ReportFor(
        HttpClient moderator, string targetType, string targetId)
    {
        var matches = (await Queue(moderator))
            .Where(r => r.TargetType == targetType && r.TargetId == targetId)
            .ToList();

        return Assert.Single(matches);
    }

    private static async Task<string> CreatePost(HttpClient client, string? content, string[]? mediaIds = null)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/posts",
            new { content, sensitivity = "General", mediaIds = mediaIds ?? Array.Empty<string>() });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PostResponse>())!.Id;
    }

    private static async Task<string> UploadReadyImage(HttpClient client, string name = "pic.png")
    {
        var file = new ByteArrayContent(TestImages.RealPng(70, 50));
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        var response = await client.PostAsync(
            "/api/v1/media", new MultipartFormDataContent { { file, "file", name } });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = (await response.Content.ReadFromJsonAsync<MediaStatusResponse>())!.Id;
        await MediaUploadFlowTests.PollStatusAsync(client, id);
        return id;
    }

    [Fact]
    public async Task A_reported_posts_text_is_captured()
    {
        var moderator = await ModeratorClient();
        var (alice, aliceName) = await RegisterClient();
        var postId = await CreatePost(alice, "something worth reporting");

        await ReportSubject(alice, $"/api/v1/posts/{postId}/report", new { category = "Harassment" });

        var report = await ReportFor(moderator, "Post", postId);
        Assert.NotNull(report.Subject);
        Assert.Equal("Text", report.Subject!.Kind);
        Assert.Equal("something worth reporting", report.Subject.Content);
        Assert.Equal(aliceName, report.Subject.AuthorUsername);
    }

    [Fact]
    public async Task The_snapshot_survives_the_subject_being_deleted()
    {
        // The point of the whole feature. Before this, deleting a post left its report pointing at
        // a row a moderator could not read.
        var moderator = await ModeratorClient();
        var (alice, _) = await RegisterClient();
        var postId = await CreatePost(alice, "this will be gone by the time you look");

        await ReportSubject(alice, $"/api/v1/posts/{postId}/report", new { category = "Harassment" });
        await alice.DeleteAsync($"/api/v1/posts/{postId}");

        var report = await ReportFor(moderator, "Post", postId);
        Assert.NotNull(report.Subject);
        Assert.Equal("this will be gone by the time you look", report.Subject!.Content);
    }

    [Fact]
    public async Task The_snapshot_is_taken_at_report_time_not_review_time()
    {
        var moderator = await ModeratorClient();
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();

        var conversation = await alice.PostAsJsonAsync("/api/v1/conversations", new { username = bobName });
        var conversationId = (await conversation.Content.ReadFromJsonAsync<ConversationResponse>())!.Id;

        var sent = await bob.PostAsJsonAsync(
            $"/api/v1/conversations/{conversationId}/messages", new { content = "the version that was reported" });
        var messageId = (await sent.Content.ReadFromJsonAsync<MessageResponse>())!.Id;

        await ReportSubject(
            alice, $"/api/v1/conversations/{conversationId}/messages/{messageId}/report",
            new { category = "Harassment" });

        // Rewritten afterwards. The report must show what it was filed against — otherwise the
        // moderator judges something different from what the reporter saw. A message is used
        // because posts have no edit endpoint at all, so this is the only subject that can move
        // under a report.
        var edit = await bob.PatchAsJsonAsync(
            $"/api/v1/conversations/{conversationId}/messages/{messageId}",
            new { content = "an entirely different, blameless message", version = 0 });
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);

        var report = await ReportFor(moderator, "Message", messageId);
        Assert.Equal("the version that was reported", report.Subject!.Content);
    }

    [Fact]
    public async Task A_reported_message_is_captured()
    {
        var moderator = await ModeratorClient();
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();

        var conversation = await alice.PostAsJsonAsync("/api/v1/conversations", new { username = bobName });
        var conversationId = (await conversation.Content.ReadFromJsonAsync<ConversationResponse>())!.Id;

        var sent = await bob.PostAsJsonAsync(
            $"/api/v1/conversations/{conversationId}/messages", new { content = "a private message" });
        var messageId = (await sent.Content.ReadFromJsonAsync<MessageResponse>())!.Id;

        await ReportSubject(
            alice, $"/api/v1/conversations/{conversationId}/messages/{messageId}/report",
            new { category = "Harassment" });

        var report = await ReportFor(moderator, "Message", messageId);
        Assert.Equal("Message", report.TargetType);
        Assert.Equal("a private message", report.Subject!.Content);
        Assert.Equal(bobName, report.Subject.AuthorUsername);
    }

    [Fact]
    public async Task A_reported_user_is_captured_by_username()
    {
        var moderator = await ModeratorClient();
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();

        await ReportSubject(alice, $"/api/v1/users/{bobName}/report", new { category = "Harassment" });
        var bobId = (await bob.GetFromJsonAsync<UserProfileResponse>($"/api/v1/users/{bobName}")).UserId;

        var report = await ReportFor(moderator, "User", bobId);
        Assert.Equal("User", report.TargetType);
        Assert.Equal("Username", report.Subject!.Kind);
        Assert.Equal(bobName, report.Subject.Content);
    }

    [Fact]
    public async Task A_reported_media_is_captured_by_its_identifying_details_not_its_bytes()
    {
        var moderator = await ModeratorClient();
        var (alice, aliceName) = await RegisterClient();
        var (carol, _) = await RegisterClient();

        var mediaId = await UploadReadyImage(alice, "evidence.png");

        // Reported by someone else: reporting your own upload is refused, same as a self-report on
        // a message.
        await ReportSubject(carol, $"/api/v1/media/{mediaId}/report", new { category = "Other" });

        var report = await ReportFor(moderator, "Media", mediaId);
        Assert.Equal("Image", report.Subject!.Kind);
        // File name and content type only. Media is soft-deleted, so the original is still
        // retrievable — copying the bytes here would be a second copy of reported content.
        Assert.Contains("evidence.png", report.Subject.Content);
        Assert.Contains("image/png", report.Subject.Content);
        Assert.Equal(aliceName, report.Subject.AuthorUsername);
    }

    [Fact]
    public async Task A_snapshot_is_purged_when_an_ordinary_report_is_resolved()
    {
        var moderator = await ModeratorClient();
        var (alice, _) = await RegisterClient();
        var postId = await CreatePost(alice, "harmless enough");
        await ReportSubject(alice, $"/api/v1/posts/{postId}/report", new { category = "Spam" });

        var report = await ReportFor(moderator, "Post", postId);
        Assert.NotNull(report.Subject);

        var action = await moderator.PostAsJsonAsync(
            $"/api/v1/admin/reports/{report.Id}/resolve",
            new { action = "Dismiss", reason = "no action needed" });
        Assert.Equal(HttpStatusCode.NoContent, action.StatusCode);

        // Asserted on the stored row, not the queue: a resolved report has left the queue.
        var after = await StoredReportAsync(report.Id);
        // Once a moderator has looked, there is no reason to keep a copy of somebody's post.
        Assert.Null(after.SubjectContent);
        Assert.Null(after.SubjectKind);
    }

    [Fact]
    public async Task A_snapshot_is_KEPT_when_an_illegal_report_is_resolved()
    {
        // The carve-out: for these categories the snapshot is the only remaining record of what
        // was reported, so destroying it on resolution would destroy the record of an offence.
        var moderator = await ModeratorClient();
        var (alice, _) = await RegisterClient();
        var postId = await CreatePost(alice, "evidence that must outlive the report");
        await ReportSubject(alice, $"/api/v1/posts/{postId}/report", new { category = "InvolvesMinor" });

        var report = await ReportFor(moderator, "Post", postId);
        var action = await moderator.PostAsJsonAsync(
            $"/api/v1/admin/reports/{report.Id}/resolve",
            new { action = "Dismiss", reason = "reviewed" });
        Assert.Equal(HttpStatusCode.NoContent, action.StatusCode);

        var after = await StoredReportAsync(report.Id);
        Assert.Equal("evidence that must outlive the report", after.SubjectContent);
        Assert.Equal(ReportSubjectKind.Text, after.SubjectKind);
    }

    [Fact]
    public async Task A_duplicate_report_does_not_overwrite_the_original_snapshot()
    {
        var moderator = await ModeratorClient();
        var (alice, _) = await RegisterClient();
        var postId = await CreatePost(alice, "reported once, reported again");
        await ReportSubject(alice, $"/api/v1/posts/{postId}/report", new { category = "Harassment" });

        // Idempotent by design — filing twice is a silent no-op, so there is no second snapshot.
        var again = await alice.PostAsJsonAsync(
            $"/api/v1/posts/{postId}/report", new { category = "Harassment" });
        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);

        var report = await ReportFor(moderator, "Post", postId);
        Assert.Equal("reported once, reported again", report.Subject!.Content);
    }

    [Fact]
    public async Task A_long_post_is_captured_truncated_rather_than_in_full()
    {
        var moderator = await ModeratorClient();
        var (alice, _) = await RegisterClient();
        var postId = await CreatePost(alice, new string('x', 5000));
        await ReportSubject(alice, $"/api/v1/posts/{postId}/report", new { category = "Other" });

        var report = await ReportFor(moderator, "Post", postId);
        // A moderation queue needs enough to judge a report, not a complete archive. The ellipsis
        // is what tells a reviewer there is more than they are seeing.
        Assert.Equal(2001, report.Subject!.Content.Length);
        Assert.EndsWith("…", report.Subject.Content);
    }

    [Fact]
    public async Task A_media_only_post_still_gets_a_snapshot()
    {
        // No caption means no text — but the author and the fact it existed are what a queue row
        // needs, so dropping the snapshot entirely would show "content unavailable" for something
        // perfectly available.
        var moderator = await ModeratorClient();
        var (alice, _) = await RegisterClient();
        var mediaId = await UploadReadyImage(alice);

        var postId = await CreatePost(alice, null, [mediaId]);
        await ReportSubject(alice, $"/api/v1/posts/{postId}/report", new { category = "Other" });

        var report = await ReportFor(moderator, "Post", postId);
        Assert.NotNull(report.Subject);
        Assert.Equal(string.Empty, report.Subject!.Content);
        Assert.NotNull(report.Subject.AuthorUsername);
    }
}
