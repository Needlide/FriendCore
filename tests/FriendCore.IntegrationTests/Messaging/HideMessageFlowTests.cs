using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Media;
using FriendCore.Infrastructure.Persistence;
using FriendCore.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FriendCore.IntegrationTests.Messaging;

/// <summary>
///     Hiding takes a message out of ONE participant's view and leaves everything else alone.
///     These are the database-level guarantees the handler's unit tests cannot reach: that the
///     read paths actually filter, that the refusal is persisted, and that the sender's copy
///     survives untouched.
/// </summary>
public class HideMessageFlowTests(FriendCoreApiFactory factory) : IClassFixture<FriendCoreApiFactory>
{
    private Task<(HttpClient Client, string Username)> RegisterClient() =>
        new AuthScenarios(factory).RegisterVerifiedClientAsync();

    private static async Task<ConversationResponse> StartConversation(HttpClient client, string username)
    {
        var response = await client.PostAsJsonAsync("/api/v1/conversations", new { username });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ConversationResponse>())!;
    }

    private static async Task<MessageResponse> Send(HttpClient client, string conversationId, string? content)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/v1/conversations/{conversationId}/messages", new { content });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<MessageResponse>())!;
    }

    private static async Task<string?> Upload(HttpClient client, byte[] bytes)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        var response = await client.PostAsync(
            "/api/v1/media", new MultipartFormDataContent { { file, "file", "image.png" } });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = (await response.Content.ReadFromJsonAsync<MediaStatusResponse>())!.Id;
        return await Media.MediaUploadFlowTests.PollStatusAsync(client, id) == "Ready" ? id : null;
    }

    private static Task<HttpResponseMessage> Hide(HttpClient client, string conversationId, string messageId) =>
        client.PostAsync($"/api/v1/conversations/{conversationId}/messages/{messageId}/hide", null);

    private static async Task<string[]> VisibleIds(HttpClient client, string conversationId)
    {
        var feed = await client.GetFromJsonAsync<MessageFeedResponse>(
            $"/api/v1/conversations/{conversationId}/messages");
        return feed!.Messages.Select(m => m.Id).ToArray();
    }

    private async Task<T> Run<T>(Func<AppDbContext, Task<T>> query)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await query(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    [Fact]
    public async Task Hiding_removes_it_from_the_hiders_thread_only()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);
        var theirs = await Send(alice, conversation.Id, "you can hide this");
        var mine = await Send(bob, conversation.Id, "but not this");

        Assert.Equal(HttpStatusCode.NoContent, (await Hide(alice, conversation.Id, theirs.Id)).StatusCode);

        Assert.Equal([mine.Id], await VisibleIds(alice, conversation.Id));

        // The load-bearing difference from delete: their copy is untouched, text and all.
        // Newest-first, as the feed always is.
        Assert.Equal([mine.Id, theirs.Id], await VisibleIds(bob, conversation.Id));
    }

    [Fact]
    public async Task Hiding_leaves_the_message_row_and_its_text_alone()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);
        var message = await Send(alice, conversation.Id, "not scrubbed");

        await Hide(bob, conversation.Id, message.Id);

        var stored = await this.Run(async db => await db.Messages
            .IgnoreQueryFilters()
            .SingleAsync(m => m.Id == Ulid.Parse(message.Id)));

        Assert.Null(stored.DeletedAt);
        Assert.Equal("not scrubbed", stored.Content);
    }

    [Fact]
    public async Task Hiding_an_image_records_a_refusal_that_stops_a_resend()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);

        // A real decodable PNG: the upload validates the content, so arbitrary bytes are a 400.
        // Sized uniquely because the container database is shared across the class, and a refusal
        // is scoped by content hash — a byte-identical image would inherit another test's refusal.
        var bytes = TestImages.RealPng(64, 48);
        var mediaId = await Upload(alice, bytes);
        Assert.NotNull(mediaId);

        var sent = await alice.PostAsJsonAsync(
            $"/api/v1/conversations/{conversation.Id}/messages", new { mediaId });
        Assert.Equal(HttpStatusCode.Created, sent.StatusCode);
        var message = (await sent.Content.ReadFromJsonAsync<MessageResponse>())!;

        Assert.Equal(HttpStatusCode.NoContent, (await Hide(bob, conversation.Id, message.Id)).StatusCode);

        // The end-to-end promise, stated as the sender experiences it: the same bytes cannot come
        // back. Re-uploaded rather than reusing `mediaId`, which is now attached to the hidden
        // message — sending it again would be rejected as already-attached, and the test would
        // pass for the wrong reason.
        var again = await alice.PostAsJsonAsync(
            $"/api/v1/conversations/{conversation.Id}/messages",
            new { mediaId = await Upload(alice, bytes) });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        // Scoped to the pair, not to the file: Carol never declined anything.
        var (carol, carolName) = await RegisterClient();
        var withCarol = await StartConversation(alice, carolName);
        var toCarol = await alice.PostAsJsonAsync(
            $"/api/v1/conversations/{withCarol.Id}/messages",
            new { mediaId = await Upload(alice, bytes) });
        Assert.Equal(HttpStatusCode.Created, toCarol.StatusCode);
    }

    [Fact]
    public async Task Hiding_twice_is_a_no_op_rather_than_an_error()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);
        var message = await Send(alice, conversation.Id, "idempotent");

        Assert.Equal(HttpStatusCode.NoContent, (await Hide(bob, conversation.Id, message.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Hide(bob, conversation.Id, message.Id)).StatusCode);

        // One row, not two: the composite key would otherwise turn a double-tap into a 500.
        Assert.Equal(1, await this.Run(async db => await db.MessageHidden.CountAsync(h => h.MessageId == Ulid.Parse(message.Id))));
    }

    [Fact]
    public async Task Hiding_is_refused_to_a_non_participant()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var (carol, _) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);
        var message = await Send(alice, conversation.Id, "not yours");

        Assert.Equal(HttpStatusCode.NotFound, (await Hide(carol, conversation.Id, message.Id)).StatusCode);
        Assert.Equal([message.Id], await VisibleIds(alice, conversation.Id));
    }

    [Fact]
    public async Task A_hidden_message_keeps_being_reportable_by_the_people_who_can_still_see_it()
    {
        // The reason hiding is not a delete: a report filed against the message still resolves,
        // because the row the moderator needs is still there.
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);
        var message = await Send(alice, conversation.Id, "reportable afterwards");

        await Hide(bob, conversation.Id, message.Id);

        // By Bob, not Alice: Alice sent it, and the server refuses a self-report — so reporting
        // from the sender would be a 400 and would prove nothing about the hidden row at all.
        var report = await bob.PostAsJsonAsync(
            $"/api/v1/conversations/{conversation.Id}/messages/{message.Id}/report",
            new { category = "Harassment" });
        Assert.Equal(HttpStatusCode.NoContent, report.StatusCode);

        var stored = await this.Run(async db => await db.Messages.IgnoreQueryFilters()
            .SingleAsync(m => m.Id == Ulid.Parse(message.Id)));
        Assert.Null(stored.DeletedAt);
    }
}