using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FriendCore.Application.Common.Models;
using FriendCore.IntegrationTests.Infrastructure;
using FriendCore.IntegrationTests.Media;

namespace FriendCore.IntegrationTests.Messaging;

/// <summary>
///     DMs with attachments end to end: upload → process → send as the only content of a
///     message → read back through the thread, with the same variant URLs and safety flags
///     a post gets. The point of the reuse is that nothing here is DM-specific: the media
///     pipeline, its storage, and its moderation are entirely the post ones.
/// </summary>
public class MessageMediaFlowTests(FriendCoreApiFactory factory) : IClassFixture<FriendCoreApiFactory>
{
    private Task<(HttpClient Client, string Username)> RegisterClient() =>
        new AuthScenarios(factory).RegisterVerifiedClientAsync();

    private static async Task<string> UploadReadyMedia(HttpClient client, bool flashing = false, int width = 200)
    {
        var file = new ByteArrayContent(TestImages.RealPng(width, 100));
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        var form = new MultipartFormDataContent
        {
            { file, "file", "image.png" },
            {
                new StringContent(JsonSerializer.Serialize(new Dictionary<string, bool>
                {
                    ["flashing"] = flashing,
                    ["intenseMotion"] = false
                })),
                "SafetyFlags"
            }
        };

        var response = await client.PostAsync("/api/v1/media", form);
        var body = await response.Content.ReadFromJsonAsync<MediaStatusResponse>();
        Assert.Equal("Ready", await MediaUploadFlowTests.PollStatusAsync(client, body!.Id));
        return body.Id;
    }

    private static async Task<ConversationResponse> StartConversation(HttpClient client, string username) =>
        (await client.PostAsJsonAsync("/api/v1/conversations", new { username }))
            .Content.ReadFromJsonAsync<ConversationResponse>().Result;

    [Fact]
    public async Task SendAttachmentOnly_Succeeds_WithNullContent_AndMediaInTheThread()
    {
        var (alice, _) = await RegisterClient();
        var (_, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);
        var mediaId = await UploadReadyMedia(alice);

        var sent = await alice.PostAsJsonAsync(
            $"/api/v1/conversations/{conversation.Id}/messages", new { content = (string?)null, mediaId });
        Assert.Equal(HttpStatusCode.Created, sent.StatusCode);

        var message = await sent.Content.ReadFromJsonAsync<MessageResponse>();
        // No "Sent a file" placeholder: the bubble renders from Media or not at all.
        Assert.Null(message!.Content);
        var image = Assert.Single(message.Media);
        Assert.Equal(mediaId, image.Id);
        Assert.DoesNotContain(image.Variants, v => v.Type == "Original");
        Assert.Equal(4, image.Variants.Count);
        var medium = image.Variants.Single(v => v.Type == "Medium");
        Assert.EndsWith($"uploads/variants/{mediaId}/medium.webp", medium.Url);
        Assert.StartsWith("http", medium.Url);

        // The recipient sees the same payload, hydrated on the read path.
        var thread = await alice.GetFromJsonAsync<MessageFeedResponse>(
            $"/api/v1/conversations/{conversation.Id}/messages");
        var echoed = thread!.Messages.Single();
        Assert.Null(echoed.Content);
        Assert.Equal(mediaId, Assert.Single(echoed.Media).Id);
    }

    [Fact]
    public async Task SendAttachmentWithCaption_KeepsBoth()
    {
        var (alice, _) = await RegisterClient();
        var (_, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);
        var mediaId = await UploadReadyMedia(alice);

        var sent = await alice.PostAsJsonAsync(
            $"/api/v1/conversations/{conversation.Id}/messages", new { content = "look", mediaId });
        var message = await sent.Content.ReadFromJsonAsync<MessageResponse>();

        Assert.Equal(HttpStatusCode.Created, sent.StatusCode);
        Assert.Equal("look", message!.Content);
        Assert.Equal(mediaId, Assert.Single(message.Media).Id);
    }

    [Fact]
    public async Task SendFourImages_PreservesDisplayOrder_AndAllComeBack()
    {
        var (alice, _) = await RegisterClient();
        var (_, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);

        // Distinct dimensions keep the PNG bytes distinct, so nothing here depends on one
        // upload deduplicating against another.
        var mediaIds = new List<string>();
        for (var index = 0; index < 4; index++)
            mediaIds.Add(await UploadReadyMedia(alice, width: 200 + index));

        var sent = await alice.PostAsJsonAsync(
            $"/api/v1/conversations/{conversation.Id}/messages",
            new { content = "album", mediaIds });
        Assert.Equal(HttpStatusCode.Created, sent.StatusCode);

        var message = (await sent.Content.ReadFromJsonAsync<MessageResponse>())!;
        Assert.Equal(mediaIds, message.Media.Select(image => image.Id));

        // The read path groups the junction rows by message and keeps display order.
        var thread = await alice.GetFromJsonAsync<MessageFeedResponse>(
            $"/api/v1/conversations/{conversation.Id}/messages");
        Assert.Equal(mediaIds, thread!.Messages.Single().Media.Select(image => image.Id));
    }

    [Fact]
    public async Task MoreThanFourImages_ReturnsBadRequest()
    {
        var (alice, _) = await RegisterClient();
        var (_, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);

        var mediaIds = new List<string>();
        for (var index = 0; index < 5; index++)
            mediaIds.Add(await UploadReadyMedia(alice, width: 220 + index));

        var response = await alice.PostAsJsonAsync(
            $"/api/v1/conversations/{conversation.Id}/messages", new { mediaIds });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TheSameImageTwice_ReturnsBadRequest()
    {
        var (alice, _) = await RegisterClient();
        var (_, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);
        var mediaId = await UploadReadyMedia(alice);

        var response = await alice.PostAsJsonAsync(
            $"/api/v1/conversations/{conversation.Id}/messages",
            new { mediaIds = new[] { mediaId, mediaId } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task OneForeignImage_RefusesTheWholeSet_RatherThanDroppingIt()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);

        var alicesImage = await UploadReadyMedia(alice);
        var bobsImage = await UploadReadyMedia(bob, width: 240);

        var response = await alice.PostAsJsonAsync(
            $"/api/v1/conversations/{conversation.Id}/messages",
            new { content = "mine and theirs", mediaIds = new[] { alicesImage, bobsImage } });

        // All-or-nothing: a 201 here would silently send only the first image.
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TextOnlyMessage_HasNoMedia_AndStillWorks()
    {
        var (alice, _) = await RegisterClient();
        var (_, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);

        var sent = await alice.PostAsJsonAsync(
            $"/api/v1/conversations/{conversation.Id}/messages", new { content = "just text" });
        var message = await sent.Content.ReadFromJsonAsync<MessageResponse>();

        Assert.Equal(HttpStatusCode.Created, sent.StatusCode);
        Assert.Equal("just text", message!.Content);
        Assert.Empty(message.Media);
    }

    [Fact]
    public async Task NeitherTextNorAttachment_ReturnsBadRequest()
    {
        var (alice, _) = await RegisterClient();
        var (_, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);

        var response = await alice.PostAsJsonAsync(
            $"/api/v1/conversations/{conversation.Id}/messages",
            new { content = "   ", mediaId = (string?)null });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SomeoneElsesMedia_CannotBeAttached()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);
        var bobsMedia = await UploadReadyMedia(bob);

        var response = await alice.PostAsJsonAsync(
            $"/api/v1/conversations/{conversation.Id}/messages", new { content = "borrowed", mediaId = bobsMedia });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// The rule is "an upload is attachable once, anywhere" — not once per aggregate. Without
    /// the union check the same image would go out in a DM and then silently also appear on
    /// a post.
    /// </summary>
    [Fact]
    public async Task MediaAlreadyInAMessage_CannotThenBeAttachedToAPost()
    {
        var (alice, _) = await RegisterClient();
        var (_, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);
        var mediaId = await UploadReadyMedia(alice);

        var sent = await alice.PostAsJsonAsync(
            $"/api/v1/conversations/{conversation.Id}/messages", new { content = (string?)null, mediaId });
        Assert.Equal(HttpStatusCode.Created, sent.StatusCode);

        var post = await alice.PostAsJsonAsync(
            "/api/v1/posts", new { content = "recycled", sensitivity = "General", mediaIds = new[] { mediaId } });

        Assert.Equal(HttpStatusCode.Conflict, post.StatusCode);
    }

    [Fact]
    public async Task ReusingAnAttachment_ReturnsConflict()
    {
        var (alice, _) = await RegisterClient();
        var (_, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);
        var mediaId = await UploadReadyMedia(alice);

        await alice.PostAsJsonAsync(
            $"/api/v1/conversations/{conversation.Id}/messages", new { content = (string?)null, mediaId });
        var again = await alice.PostAsJsonAsync(
            $"/api/v1/conversations/{conversation.Id}/messages", new { content = "again", mediaId });

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task WellFormedButUnknownMedia_ReturnsNotFound()
    {
        var (alice, _) = await RegisterClient();
        var (_, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);

        var response = await alice.PostAsJsonAsync(
            $"/api/v1/conversations/{conversation.Id}/messages",
            new { content = "ghost", mediaId = Ulid.NewUlid().ToString() });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Coercing a malformed id to "no attachment" would return 201 and drop the picture
    /// while the caller believed it was sent.
    /// </summary>
    [Fact]
    public async Task MalformedMediaId_ReturnsBadRequest_RatherThanSendingTextOnly()
    {
        var (alice, _) = await RegisterClient();
        var (_, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);

        var response = await alice.PostAsJsonAsync(
            $"/api/v1/conversations/{conversation.Id}/messages",
            new { content = "look", mediaId = "not-a-ulid" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Deleting the media would leave the bubble with nothing in it, so it is refused — the
    /// same guard posts get, now covering DMs too.
    /// </summary>
    [Fact]
    public async Task MediaAttachedToAMessage_CannotBeDeleted()
    {
        var (alice, _) = await RegisterClient();
        var (_, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);
        var mediaId = await UploadReadyMedia(alice);

        await alice.PostAsJsonAsync(
            $"/api/v1/conversations/{conversation.Id}/messages", new { content = (string?)null, mediaId });

        var delete = await alice.DeleteAsync($"/api/v1/media/{mediaId}");

        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
    }

    [Fact]
    public async Task FlaggedAttachment_ArrivesWithNoVariants_AndTheRecipientCanRevealIt()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);
        var mediaId = await UploadReadyMedia(alice, flashing: true);

        var sent = await alice.PostAsJsonAsync(
            $"/api/v1/conversations/{conversation.Id}/messages", new { content = (string?)null, mediaId });
        Assert.Equal(HttpStatusCode.Created, sent.StatusCode);

        // Bob's thread response hands him no URLs at all — the gate is server-side, so it does
        // not matter which client renders the bubble.
        var feed = await bob.GetFromJsonAsync<MessageFeedResponse>(
            $"/api/v1/conversations/{conversation.Id}/messages");
        var bubble = feed!.Messages.Single(m => m.Media.Any(image => image.Id == mediaId));
        var gated = bubble.Media.Single(image => image.Id == mediaId);
        Assert.Empty(gated.Variants);
        Assert.True(gated.SafetyFlags["flashing"]);

        // Consent, from a conversation participant, succeeds.
        var reveal = await bob.PostAsync($"/api/v1/media/{mediaId}/reveal", content: null);
        Assert.Equal(HttpStatusCode.OK, reveal.StatusCode);
        var revealed = await reveal.Content.ReadFromJsonAsync<MediaVariantsResponse>();
        Assert.NotEmpty(revealed!.Variants);
    }

    [Fact]
    public async Task Outsider_CannotReveal_ADMImage_EvenWithTheMediaId()
    {
        // The case the whole reveal design exists for. A DM image is visible to participants
        // only, and the media id alone must not be enough to unlock it.
        var (alice, aliceName) = await RegisterClient();
        var (bob, _) = await RegisterClient();
        var (mallory, _) = await RegisterClient();
        var (_, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);
        var mediaId = await UploadReadyMedia(alice, flashing: true);

        var sent = await alice.PostAsJsonAsync(
            $"/api/v1/conversations/{conversation.Id}/messages", new { content = (string?)null, mediaId });
        Assert.Equal(HttpStatusCode.Created, sent.StatusCode);

        // Not a participant, and cannot read the thread either.
        var reveal = await mallory.PostAsync($"/api/v1/media/{mediaId}/reveal", content: null);
        Assert.Equal(HttpStatusCode.NotFound, reveal.StatusCode);

        var thread = await mallory.GetAsync($"/api/v1/conversations/{conversation.Id}/messages");
        Assert.Equal(HttpStatusCode.NotFound, thread.StatusCode);
        Assert.NotNull(aliceName);
    }
}
