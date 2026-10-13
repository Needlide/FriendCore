using System.Net;
using System.Net.Http.Json;
using FriendCore.Application.Common.Models;
using FriendCore.IntegrationTests.Infrastructure;

namespace FriendCore.IntegrationTests.Messaging;

/// <summary>
///     Jumping from a search result to where the message actually sits. The interesting cases are
///     the edges: a message at the very start or end of a conversation, and the cursors that have
///     to let the reader carry on in either direction.
/// </summary>
public class JumpToMessageFlowTests(FriendCoreApiFactory factory) : IClassFixture<FriendCoreApiFactory>
{
    private Task<(HttpClient Client, string Username)> RegisterClient() =>
        new AuthScenarios(factory).RegisterVerifiedClientAsync();

    private static async Task<ConversationResponse> StartConversation(HttpClient client, string username)
    {
        var response = await client.PostAsJsonAsync("/api/v1/conversations", new { username });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ConversationResponse>())!;
    }

    private static async Task<MessageResponse> Send(HttpClient client, string conversationId, string content)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/v1/conversations/{conversationId}/messages", new { content });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<MessageResponse>())!;
    }

    private static async Task<MessageWindowResponse> Around(HttpClient client, string conversationId, string messageId)
    {
        var response = await client.GetAsync(
            $"/api/v1/conversations/{conversationId}/messages/{messageId}/around");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<MessageWindowResponse>())!;
    }

    /// <summary>Sends <paramref name="count" /> numbered messages, oldest first.</summary>
    private static async Task<List<MessageResponse>> Conversation(HttpClient client, string conversationId, int count)
    {
        var sent = new List<MessageResponse>();
        for (var i = 1; i <= count; i++)
            sent.Add(await Send(client, conversationId, $"message {i}"));
        return sent;
    }

    [Fact]
    public async Task The_target_message_is_in_the_window()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);
        await Conversation(alice, conversation.Id, 5);

        var window = await Around(bob, conversation.Id, (await Send(alice, conversation.Id, "the one")).Id);

        Assert.Contains(window.Messages, m => m.Content == "the one");
    }

    [Fact]
    public async Task The_window_is_oldest_first_with_context_around_the_target()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);
        await Conversation(alice, conversation.Id, 5);

        var target = await Send(alice, conversation.Id, "the one");
        var window = await Around(bob, conversation.Id, target.Id);

        // Context on both sides, and in timeline order — the whole point of jumping rather than
        // being handed a lone message.
        Assert.Contains(window.Messages, m => m.Content == "message 5");
        Assert.Contains(window.Messages, m => m.Content == "the one");

        var timestamps = window.Messages.Select(m => m.CreatedAt).ToList();
        Assert.Equal(timestamps.OrderBy(t => t).ToList(), timestamps);
    }

    /// <summary>
    ///     Both edge tests need more messages than the 25-message radius, or the window reaches the
    ///     far end of the conversation and there is nothing left to page to on EITHER side — which
    ///     looks identical to "the target is at the edge" and would make the test pass for the
    ///     wrong reason.
    /// </summary>
    [Fact]
    public async Task A_message_at_the_start_has_nothing_older()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);
        var first = (await Conversation(alice, conversation.Id, 60))[0];

        var window = await Around(bob, conversation.Id, first.Id);

        Assert.Null(window.OlderCursor);
        // Still 25 to read forward to, so that side keeps its cursor.
        Assert.NotNull(window.NewerCursor);
    }

    [Fact]
    public async Task A_message_at_the_end_has_nothing_newer()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);
        var last = (await Conversation(alice, conversation.Id, 60))[^1];

        var window = await Around(bob, conversation.Id, last.Id);

        Assert.Null(window.NewerCursor);
        Assert.NotNull(window.OlderCursor);
    }

    [Fact]
    public async Task A_short_conversation_produces_no_cursors_at_all()
    {
        // The other face of the edge case: when the window swallows the whole conversation there is
        // nothing to page to, and the client must not be offered a cursor that leads nowhere.
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);
        var only = (await Conversation(alice, conversation.Id, 3))[1];

        var window = await Around(bob, conversation.Id, only.Id);

        Assert.Equal(3, window.Messages.Count);
        Assert.Null(window.OlderCursor);
        Assert.Null(window.NewerCursor);
    }

    [Fact]
    public async Task The_older_cursor_pages_further_back()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);
        // More than the 25 either side, so both edges have somewhere to go.
        var sent = await Conversation(alice, conversation.Id, 60);

        var window = await Around(bob, conversation.Id, sent[40].Id);
        Assert.NotNull(window.OlderCursor);

        var older = await bob.GetFromJsonAsync<MessageFeedResponse>(
            $"/api/v1/conversations/{conversation.Id}/messages?cursor={window.OlderCursor}");

        // Continuing from the window's edge must reach further back, and must not repeat anything
        // already on screen.
        Assert.Contains(older!.Messages, m => m.Id == sent[0].Id);
        Assert.DoesNotContain(older.Messages, m => m.Id == sent[40].Id);
    }

    [Fact]
    public async Task A_hidden_message_is_not_reachable_by_jumping_to_it()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);
        var target = await Send(alice, conversation.Id, "please forget this");

        await bob.PostAsync($"/api/v1/conversations/{conversation.Id}/messages/{target.Id}/hide", null);

        // Otherwise a search result the reader never cleared would still be reachable by id, and
        // hiding would only be hiding from the list.
        var response = await bob.GetAsync(
            $"/api/v1/conversations/{conversation.Id}/messages/{target.Id}/around");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // Alice still has hers.
        Assert.Equal(HttpStatusCode.OK, (await alice.GetAsync(
            $"/api/v1/conversations/{conversation.Id}/messages/{target.Id}/around")).StatusCode);
    }

    [Fact]
    public async Task A_deleted_message_is_not_reachable_by_jumping_to_it()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);
        var target = await Send(alice, conversation.Id, "delete me");

        await alice.DeleteAsync($"/api/v1/conversations/{conversation.Id}/messages/{target.Id}");

        var response = await bob.GetAsync(
            $"/api/v1/conversations/{conversation.Id}/messages/{target.Id}/around");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_message_from_another_conversation_is_not_found()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var (carol, carolName) = await RegisterClient();

        var ab = await StartConversation(alice, bobName);
        var ac = await StartConversation(alice, carolName);
        var inOther = await Send(alice, ac.Id, "not this conversation");

        var response = await bob.GetAsync(
            $"/api/v1/conversations/{ab.Id}/messages/{inOther.Id}/around");

        // 404, not 400: an id that exists elsewhere must be indistinguishable from one that does
        // not exist at all.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_malformed_id_is_the_same_not_found_as_an_unknown_one()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);

        var malformed = await bob.GetAsync(
            $"/api/v1/conversations/{conversation.Id}/messages/not-a-ulid/around");
        var unknown = await bob.GetAsync(
            $"/api/v1/conversations/{conversation.Id}/messages/{Guid.NewGuid():D}/around");

        Assert.Equal(HttpStatusCode.NotFound, malformed.StatusCode);
        Assert.Equal(malformed.StatusCode, unknown.StatusCode);
    }

    [Fact]
    public async Task Jumping_is_refused_to_a_non_participant()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var (carol, _) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);
        var target = await Send(alice, conversation.Id, "private");

        var response = await carol.GetAsync(
            $"/api/v1/conversations/{conversation.Id}/messages/{target.Id}/around");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Jumping_is_refused_across_a_block()
    {
        var (alice, aliceName) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);
        var target = await Send(alice, conversation.Id, "before the block");

        var block = await bob.PostAsJsonAsync($"/api/v1/users/{aliceName}/block", new { });
        Assert.True(
            block.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.Created,
            $"blocking failed: {block.StatusCode}");

        var response = await alice.GetAsync(
            $"/api/v1/conversations/{conversation.Id}/messages/{target.Id}/around");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task The_window_carries_media_reactions_and_reply_targets()
    {
        // Same as the feed: a jumped-to message must render as an ordinary bubble.
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);

        var parent = await Send(alice, conversation.Id, "something to reply to");

        // Actually a reply, not merely the next message along: the assertion below is about the
        // reply target resolving through this endpoint, and a plain message would leave it null
        // and fail for a reason that has nothing to do with the window.
        var replyResponse = await bob.PostAsJsonAsync(
            $"/api/v1/conversations/{conversation.Id}/messages",
            new { content = "a reply about kites", replyToMessageId = parent.Id });
        Assert.Equal(HttpStatusCode.Created, replyResponse.StatusCode);
        var target = (await replyResponse.Content.ReadFromJsonAsync<MessageResponse>())!;

        // Asserted rather than fire-and-forget: an emoji off the server's allow-list answers 400,
        // and the reaction assertions below would then fail far from the cause.
        var reaction = await alice.PostAsJsonAsync(
            $"/api/v1/conversations/{conversation.Id}/messages/{target.Id}/reactions",
            new { emoji = "👍" });
        Assert.Equal(HttpStatusCode.OK, reaction.StatusCode);

        var window = await Around(alice, conversation.Id, target.Id);

        var found = window.Messages.Single(m => m.Id == target.Id);
        Assert.NotNull(found.Reactions);
        Assert.Contains(found.Reactions, reaction => reaction.Count == 1);
        Assert.NotNull(found.ReplyTo);
        Assert.Equal(parent.Id, found.ReplyTo!.Id);
    }
}