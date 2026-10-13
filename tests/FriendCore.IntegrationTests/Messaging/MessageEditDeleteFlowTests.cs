using System.Net;
using System.Net.Http.Json;
using FriendCore.Application.Common.Models;
using FriendCore.IntegrationTests.Infrastructure;

namespace FriendCore.IntegrationTests.Messaging;

/// <summary>
/// Editing and deleting your own message. The interesting cases are all negative: the
/// recipient cannot touch it, an outsider learns nothing, and a malformed id is a 404 rather
/// than a 400 so probing cannot distinguish real ids.
/// </summary>
public class MessageEditDeleteFlowTests(FriendCoreApiFactory factory) : IClassFixture<FriendCoreApiFactory>
{
    private Task<(HttpClient Client, string Username)> RegisterClient() =>
        new AuthScenarios(factory).RegisterVerifiedClientAsync();

    private static async Task<ConversationResponse> StartConversation(HttpClient client, string username)
    {
        var response = await client.PostAsJsonAsync("/api/v1/conversations", new { username });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ConversationResponse>())!;
    }

    private static async Task<MessageResponse> SendMessage(HttpClient client, string conversationId, string content)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/v1/conversations/{conversationId}/messages", new { content });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<MessageResponse>())!;
    }

    /// <summary>
    /// The version defaults to 0 because these tests' messages have never been edited, and a
    /// freshly sent message starts at 0. Tests that edit twice must thread the returned
    /// version through instead of leaving it at the default.
    /// </summary>
    private static Task<HttpResponseMessage> Edit(
        HttpClient client, string conversationId, string messageId, string content, int version = 0) =>
        client.PatchAsJsonAsync(
            $"/api/v1/conversations/{conversationId}/messages/{messageId}", new { content, version });

    private static Task<HttpResponseMessage> Delete(HttpClient client, string conversationId, string messageId) =>
        client.DeleteAsync($"/api/v1/conversations/{conversationId}/messages/{messageId}");

    private static async Task<string?> ContentOf(HttpClient client, string conversationId, string messageId)
    {
        var feed = await client.GetFromJsonAsync<MessageFeedResponse>(
            $"/api/v1/conversations/{conversationId}/messages");
        return feed!.Messages.SingleOrDefault(m => m.Id == messageId)?.Content;
    }

    [Fact]
    public async Task Edit_ReplacesTheText_ForBothParticipants()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);
        var message = await SendMessage(alice, conversation.Id, "teh cat sat");

        var response = await Edit(alice, conversation.Id, message.Id, "the cat sat");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var edited = await response.Content.ReadFromJsonAsync<MessageResponse>();
        Assert.Equal("the cat sat", edited!.Content);
        Assert.NotNull(edited.EditedAt);

        // The recipient sees the corrected wording, not the typo they first received.
        Assert.Equal("the cat sat", await ContentOf(bob, conversation.Id, message.Id));
    }

    [Fact]
    public async Task Edit_TrimsWhitespace_AndRejectsEmptyText()
    {
        var (alice, _) = await RegisterClient();
        var (_, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);
        var message = await SendMessage(alice, conversation.Id, "hello");

        var padded = await Edit(alice, conversation.Id, message.Id, "   hello again   ");
        Assert.Equal(HttpStatusCode.OK, padded.StatusCode);
        Assert.Equal("hello again", (await padded.Content.ReadFromJsonAsync<MessageResponse>())!.Content);

        // Editing to nothing is a delete, not an edit — the message keeps its text.
        var blank = await Edit(alice, conversation.Id, message.Id, "   ");
        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);
        Assert.Equal("hello again", await ContentOf(alice, conversation.Id, message.Id));
    }

    [Fact]
    public async Task Edit_IsRefused_ForAnyoneButTheSender()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);
        var message = await SendMessage(alice, conversation.Id, "mine");

        // 403, not 404: the recipient is a participant and can already see it, so there is
        // nothing to protect — and a clear refusal is actionable where a 404 is not.
        var byRecipient = await Edit(bob, conversation.Id, message.Id, "rewriting your words");
        Assert.Equal(HttpStatusCode.Forbidden, byRecipient.StatusCode);
        Assert.Equal("mine", await ContentOf(bob, conversation.Id, message.Id));
    }

    [Fact]
    public async Task Delete_RemovesTheMessage_ForBothParticipants()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);
        var keep = await SendMessage(alice, conversation.Id, "still here");
        var doomed = await SendMessage(alice, conversation.Id, "regret this");

        var response = await Delete(alice, conversation.Id, doomed.Id);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Gone for the sender AND the recipient — no tombstone is served to either.
        Assert.Null(await ContentOf(alice, conversation.Id, doomed.Id));
        Assert.Null(await ContentOf(bob, conversation.Id, doomed.Id));

        var feed = await alice.GetFromJsonAsync<MessageFeedResponse>(
            $"/api/v1/conversations/{conversation.Id}/messages");
        Assert.Equal([keep.Id], feed!.Messages.Select(m => m.Id).ToArray());
    }

    [Fact]
    public async Task Delete_IsRefused_ForTheRecipient()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);
        var message = await SendMessage(alice, conversation.Id, "not yours to remove");

        var response = await Delete(bob, conversation.Id, message.Id);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("not yours to remove", await ContentOf(alice, conversation.Id, message.Id));
    }

    [Fact]
    public async Task DeletedMessage_IsNotFound_OnASecondDeleteOrEdit()
    {
        var (alice, _) = await RegisterClient();
        var (_, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);
        var message = await SendMessage(alice, conversation.Id, "brief");

        Assert.Equal(HttpStatusCode.NoContent, (await Delete(alice, conversation.Id, message.Id)).StatusCode);

        // The soft-delete query filter hides it, so a repeat looks like it never existed.
        Assert.Equal(HttpStatusCode.NotFound, (await Delete(alice, conversation.Id, message.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Edit(alice, conversation.Id, message.Id, "back?")).StatusCode);
    }

    [Fact]
    public async Task Outsider_GetsNotFound_ForBothActions()
    {
        var (alice, _) = await RegisterClient();
        var (_, bobName) = await RegisterClient();
        var (mallory, _) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);
        var message = await SendMessage(alice, conversation.Id, "private");

        // An outsider who guesses the conversation id must not even learn the message exists.
        Assert.Equal(HttpStatusCode.NotFound, (await Delete(mallory, conversation.Id, message.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Edit(mallory, conversation.Id, message.Id, "x")).StatusCode);
    }

    [Fact]
    public async Task MalformedIds_ReturnNotFound_NotBadRequest()
    {
        var (alice, _) = await RegisterClient();

        var badConversation = await Edit(alice, "not-an-id", "also-not-an-id", "x");
        var badMessage = await Edit(alice, "01J0000000000000000000000", "nope", "x");
        var badDelete = await Delete(alice, "not-an-id", "01J0000000000000000000000");

        // 404 across the board: a 400 would confirm which segment failed to parse.
        Assert.Equal(HttpStatusCode.NotFound, badConversation.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, badMessage.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, badDelete.StatusCode);
    }

    [Fact]
    public async Task MessageFromAnotherConversation_IsNotFound()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var (_, carolName) = await RegisterClient();
        var withBob = await StartConversation(alice, bobName);
        var withCarol = await StartConversation(alice, carolName);
        var message = await SendMessage(alice, withBob.Id, "for bob only");

        // Right message, wrong conversation: the lookup is scoped, so this is a 404 rather
        // than an edit that leaks one thread's message into another.
        var response = await Edit(alice, withCarol.Id, message.Id, "leaked into another thread");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("for bob only", await ContentOf(bob, withBob.Id, message.Id));
    }

    [Fact]
    public async Task DeletedMessage_StopsCountingAsUnread()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);
        var first = await SendMessage(alice, conversation.Id, "one");
        await SendMessage(alice, conversation.Id, "two");

        var before = await bob.GetFromJsonAsync<ConversationListResponse>("/api/v1/conversations");
        Assert.Equal(2, Assert.Single(before!.Conversations).UnreadCount);

        // The recipient takes it back rather than acting on something the sender retracted.
        Assert.Equal(HttpStatusCode.NoContent, (await Delete(alice, conversation.Id, first.Id)).StatusCode);

        var after = await bob.GetFromJsonAsync<ConversationListResponse>("/api/v1/conversations");
        Assert.Equal(1, Assert.Single(after!.Conversations).UnreadCount);
    }

    [Fact]
    public async Task Edit_BumpsTheVersion_SoASecondEditCanBeBasedOnIt()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);
        var message = await SendMessage(alice, conversation.Id, "first");

        // A freshly sent message starts at 0, and that is what a client presents first.
        Assert.Equal(0, message.Version);

        var first = await alice.PatchAsJsonAsync(
            $"/api/v1/conversations/{conversation.Id}/messages/{message.Id}",
            new { content = "second", version = 0 });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var afterFirst = (await first.Content.ReadFromJsonAsync<MessageResponse>())!;
        Assert.Equal(1, afterFirst.Version);

        // The client now holds version 1, so its next edit is based on current state.
        var second = await Edit(alice, conversation.Id, message.Id, "third", afterFirst.Version);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(2, (await second.Content.ReadFromJsonAsync<MessageResponse>())!.Version);
        Assert.Equal("third", await ContentOf(bob, conversation.Id, message.Id));
    }

    [Fact]
    public async Task Edit_RefusesAStaleVersion_With409_AndLeavesTheStoredWordingAlone()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);
        var message = await SendMessage(alice, conversation.Id, "first");

        // The same account saves a reword from another device.
        await Edit(alice, conversation.Id, message.Id, "saved on the phone");

        // This tab still believes it is looking at version 0.
        var stale = await Edit(alice, conversation.Id, message.Id, "saved on the laptop");
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        // The loser does not overwrite the winner — that is the entire point.
        Assert.Equal("saved on the phone", await ContentOf(alice, conversation.Id, message.Id));
        Assert.Equal("saved on the phone", await ContentOf(bob, conversation.Id, message.Id));
    }

    [Fact]
    public async Task Edit_RefusesAMissingVersion_With400()
    {
        var (alice, _) = await RegisterClient();
        var (_, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);
        var message = await SendMessage(alice, conversation.Id, "first");

        // Answering without one would silently re-enable last-write-wins for exactly the
        // clients least likely to want that.
        var response = await alice.PatchAsJsonAsync(
            $"/api/v1/conversations/{conversation.Id}/messages/{message.Id}",
            new { content = "no version here" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("first", await ContentOf(alice, conversation.Id, message.Id));
    }

    [Fact]
    public async Task Edit_StillWorks_ForTheRecipientWatchingTheSameConflict()
    {
        // Only the sender may edit, so a recipient's attempt is a 403 regardless of version —
        // the version check must not be reachable ahead of the ownership rule.
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);
        var message = await SendMessage(alice, conversation.Id, "mine");
        await Edit(alice, conversation.Id, message.Id, "still mine");

        var response = await Edit(bob, conversation.Id, message.Id, "not mine", version: 0);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("still mine", await ContentOf(alice, conversation.Id, message.Id));
    }

    [Fact]
    public async Task Delete_IsUnaffectedByTheEditVersion()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);
        var message = await SendMessage(alice, conversation.Id, "first");

        // The asymmetry this design exists for: a delete is terminal, so it must never be
        // blocked because the sender was mid-rewrite on another device. It takes no version and
        // consults none, so there is nothing for it to lose.
        await Edit(alice, conversation.Id, message.Id, "second");

        var deleted = await alice.DeleteAsync(
            $"/api/v1/conversations/{conversation.Id}/messages/{message.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
    }
}
