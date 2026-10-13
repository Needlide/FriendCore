using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Media;
using FriendCore.Domain.Users;
using FriendCore.Infrastructure.Persistence;
using FriendCore.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FriendCore.IntegrationTests.Media;

/// <summary>
/// Consent refusal for images: once a recipient's position is recorded, that exact file cannot
/// come back from that sender to them. The interesting cases are the rule boundaries, not the
/// happy path — who deleted it, and what exactly counts as "the same image".
/// </summary>
public class MediaConsentRefusalFlowTests(FriendCoreApiFactory factory) : IClassFixture<FriendCoreApiFactory>
{
    private Task<(HttpClient Client, string Username)> RegisterClient() =>
        new AuthScenarios(factory).RegisterVerifiedClientAsync();

    private static async Task<ConversationResponse> StartConversation(HttpClient client, string username)
    {
        var response = await client.PostAsJsonAsync("/api/v1/conversations", new { username });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ConversationResponse>())!;
    }

    /// <summary>
    ///     Uploads and waits for the item to finish processing. Sending a message whose media is
    ///     still Processing is a 400 by design, so every helper here polls first — otherwise the
    ///     tests would be measuring the wrong failure.
    /// </summary>
    private static async Task<string> Upload(HttpClient client, byte[] bytes)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");

        var response = await client.PostAsync(
            "/api/v1/media", new MultipartFormDataContent { { file, "file", "image.png" } });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var id = (await response.Content.ReadFromJsonAsync<MediaStatusResponse>())!.Id;
        Assert.Equal("Ready", await MediaUploadFlowTests.PollStatusAsync(client, id));
        return id;
    }

    private static async Task<MessageResponse> SendWithImage(HttpClient client, string conversationId, string mediaId)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/v1/conversations/{conversationId}/messages", new { mediaId });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<MessageResponse>())!;
    }

    /// <summary>Upload then send, answering with the raw response so a refusal can be asserted.</summary>
    private static async Task<HttpResponseMessage> TrySendWithImage(
        HttpClient client, string conversationId, byte[] bytes)
    {
        var mediaId = await Upload(client, bytes);
        return await client.PostAsJsonAsync(
            $"/api/v1/conversations/{conversationId}/messages", new { mediaId });
    }

    /// <summary>
    ///     Reports a message and has a moderator resolve it with ContentRemoval, over the real
    ///     admin endpoint — so the whole moderation ladder is exercised, not just the deletion.
    /// </summary>
    private async Task RemoveViaModeration(HttpClient reporter, string conversationId, MessageResponse message)
    {
        var report = await reporter.PostAsJsonAsync(
            $"/api/v1/conversations/{conversationId}/messages/{message.Id}/report",
            new { category = "NudityOrSexual" });
        Assert.Equal(HttpStatusCode.NoContent, report.StatusCode);

        var reportId = await ReportIdForAsync(Ulid.Parse(message.Id));
        var moderator = await ModeratorClient();
        var resolve = await moderator.PostAsJsonAsync(
            $"/api/v1/admin/reports/{reportId}/resolve",
            new { action = "ContentRemoval", reason = "harmful image" });
        Assert.Equal(HttpStatusCode.NoContent, resolve.StatusCode);
    }

    private async Task<HttpClient> ModeratorClient()
    {
        var client = factory.CreateClient();
        var token = await new AuthScenarios(factory).RegisterWithRoleAndGetToken(
            client, AuthScenarios.UniqueEmail(), UserRole.Moderator);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private Task<Ulid> ReportIdForAsync(Ulid messageId) =>
        factory.Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>()
            .Reports.AsNoTracking()
            .Where(r => r.TargetId == messageId)
            .Select(r => r.Id)
            .FirstAsync();

    /// <summary>
    ///     Refusals for THIS test's bytes only. The container database is shared across the class,
    ///     so an unscoped "are there any refusals?" would see every earlier test's rows and fail
    ///     for the wrong reason.
    /// </summary>
    private async Task<List<MediaRefusal>> RefusalsForAsync(byte[] bytes)
    {
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes))
            .ToLowerInvariant();

        return await factory.Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>()
            .MediaRefusals.AsNoTracking()
            .Where(refusal => refusal.ContentHash == hash)
            .ToListAsync();
    }

    /// <summary>Ordinary query: a soft-deleted item drops out, like every other soft delete.</summary>
    private Task<MediaItem?> MediaByIdAsync(string mediaId) =>
        factory.Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>()
            .MediaItems.FirstOrDefaultAsync(m => m.Id == Ulid.Parse(mediaId));

    /// <summary>Moderation view, so a soft-deleted row can still be seen to have been stamped.</summary>
    private Task<MediaItem?> MediaIncludingDeletedAsync(string mediaId) =>
        factory.Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>()
            .MediaItems.IgnoreQueryFilters().FirstOrDefaultAsync(m => m.Id == Ulid.Parse(mediaId));

    [Fact]
    public async Task ModeratorRemoval_RecordsARefusal_AndBlocksThatExactImageComingBack()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);

        var bytes = TestImages.RealPng(24, 18); // 24x18: unique per test
        var message = await SendWithImage(alice, conversation.Id, await Upload(alice, bytes));
        await RemoveViaModeration(bob, conversation.Id, message);

        Assert.Single(await RefusalsForAsync(bytes));

        // Alice re-uploading the identical file is now refused.
        var response = await TrySendWithImage(alice, conversation.Id, bytes);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ASenderDeletingTheirOwnMessage_RecordsNoRefusal()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);

        var bytes = TestImages.RealPng(29, 23);
        var message = await SendWithImage(alice, conversation.Id, await Upload(alice, bytes));
        await alice.DeleteAsync($"/api/v1/conversations/{conversation.Id}/messages/{message.Id}");

        // Withdrawing your own message is not anyone declining to receive something. Recording it
        // would permanently stop Alice re-sharing that exact image with Bob — pure friction, and
        // it protects nothing: the abuse case is BOB deleting, which records on the first pass.
        Assert.Empty(await RefusalsForAsync(bytes));

        // So she may send the identical file again, and it works.
        Assert.Equal(HttpStatusCode.Created, (await TrySendWithImage(alice, conversation.Id, bytes)).StatusCode);
    }

    [Fact]
    public async Task ADifferentImage_IsNeverRefused()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);

        // Distinct dimensions per test: RealPng is a solid-colour image, so the same dimensions
        // produce byte-identical files and every test would share a hash.
        var first = TestImages.RealPng(41, 33);
        var second = TestImages.RealPng(41, 34);

        var message = await SendWithImage(alice, conversation.Id, await Upload(alice, first));
        await RemoveViaModeration(bob, conversation.Id, message);
        Assert.Single(await RefusalsForAsync(first));

        // Scoped to the CONTENT, not to the sender: a different photo is a different matter.
        Assert.Equal(HttpStatusCode.Created, (await TrySendWithImage(alice, conversation.Id, second)).StatusCode);
    }

    [Fact]
    public async Task ARefusedUpload_IsDeletedRatherThanLeftOrphaned()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);

        var bytes = TestImages.RealPng(37, 29);
        var message = await SendWithImage(alice, conversation.Id, await Upload(alice, bytes));
        await RemoveViaModeration(bob, conversation.Id, message);

        var refusedId = await Upload(alice, bytes);
        var response = await alice.PostAsJsonAsync(
            $"/api/v1/conversations/{conversation.Id}/messages", new { mediaId = refusedId });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        // Best-effort cleanup, so a sender who keeps retrying does not accumulate one unattached
        // upload per attempt with nothing to reclaim them. Soft-deleted, not hard — the posture
        // used everywhere else in this codebase.
        Assert.Null(await MediaByIdAsync(refusedId));
        Assert.NotNull((await MediaIncludingDeletedAsync(refusedId))?.DeletedAt);
    }

    [Fact]
    public async Task TheSameImage_IsStillSendable_ToSomebodyElse()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var (carol, carolName) = await RegisterClient();

        var aliceToBob = await StartConversation(alice, bobName);
        var aliceToCarol = await StartConversation(alice, carolName);

        var bytes = TestImages.RealPng(43, 31);
        var message = await SendWithImage(alice, aliceToBob.Id, await Upload(alice, bytes));
        await RemoveViaModeration(bob, aliceToBob.Id, message);

        // Blocked for Bob. Carol never declined anything — a refusal is between two people, not a
        // global ban on a file.
        Assert.Equal(HttpStatusCode.Conflict, (await TrySendWithImage(alice, aliceToBob.Id, bytes)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await TrySendWithImage(alice, aliceToCarol.Id, bytes)).StatusCode);
    }
}