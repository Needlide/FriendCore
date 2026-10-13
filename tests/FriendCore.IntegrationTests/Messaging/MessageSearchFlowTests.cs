using System.Net;
using System.Net.Http.Json;
using FriendCore.Application.Common.Models;
using FriendCore.IntegrationTests.Infrastructure;

namespace FriendCore.IntegrationTests.Messaging;

/// <summary>
///     Finding an old message in a conversation. The cases that matter are the boundaries — what
///     counts as a match, and what must stay unfindable — rather than the happy path.
/// </summary>
public class MessageSearchFlowTests(FriendCoreApiFactory factory) : IClassFixture<FriendCoreApiFactory>
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

    private static async Task<MessageFeedResponse> Search(HttpClient client, string conversationId, string? term)
    {
        var url = $"/api/v1/conversations/{conversationId}/messages/search?q={Uri.EscapeDataString(term ?? "")}";
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<MessageFeedResponse>())!;
    }

    [Fact]
    public async Task Finds_a_message_by_a_word_in_it()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);

        // Only the middle message contains the term — the others must NOT, or the test would be
        // asserting a count rather than a match.
        await Send(alice, conversation.Id, "are we still on for friday");
        var wanted = await Send(bob, conversation.Id, "yes, I will bring the thing");
        await Send(alice, conversation.Id, "great, see you then");

        var results = await Search(bob, conversation.Id, "I will bring");

        Assert.Equal([wanted.Id], results.Messages.Select(m => m.Id).ToArray());
    }

    [Fact]
    public async Task Ignores_case()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);

        var message = await Send(alice, conversation.Id, "Capitalisation Matters");

        Assert.Equal([message.Id], (await Search(bob, conversation.Id, "capitalisation")).Messages
            .Select(m => m.Id).ToArray());
    }

    [Fact]
    public async Task Matches_a_phrase_rather_than_each_word_apart()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);

        // Both messages contain both words; only one contains them ADJACENTLY. A user typing a
        // remembered phrase wants the phrase, not the bag of words it came from.
        await Send(alice, conversation.Id, "about the friday thing");
        var contiguous = await Send(bob, conversation.Id, "see you about friday");

        var results = await Search(alice, conversation.Id, "about friday");

        Assert.Equal([contiguous.Id], results.Messages.Select(m => m.Id).ToArray());
    }

    [Fact]
    public async Task Returns_newest_first_like_the_feed()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);

        await Send(alice, conversation.Id, "needle one");
        await Send(alice, conversation.Id, "needle two");
        var last = await Send(alice, conversation.Id, "needle three");

        var results = await Search(bob, conversation.Id, "needle");

        Assert.Equal(last.Id, results.Messages[0].Id);
        Assert.Equal(3, results.Messages.Count);
    }

    [Fact]
    public async Task A_one_character_term_is_refused_rather_than_returning_everything()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);
        await Send(alice, conversation.Id, "a a a a a a a");

        // Its own error code, so the client can say "type at least two characters" instead of
        // showing an empty result the user cannot interpret.
        var response = await bob.GetAsync(
            $"/api/v1/conversations/{conversation.Id}/messages/search?q=a");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Messaging.SearchTermTooShort", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_blank_term_is_refused_too()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);

        var response = await bob.GetAsync(
            $"/api/v1/conversations/{conversation.Id}/messages/search?q=%20%20");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task LIKE_wildcards_in_the_term_are_matched_literally()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);

        await Send(alice, conversation.Id, "it costs 50% of nothing");
        await Send(bob, conversation.Id, "and 100% of something");
        await Send(alice, conversation.Id, "unrelated chatter");

        // Unescaped, "%" would match every message in the thread. This is the whole reason the
        // repository escapes before handing the term to the provider.
        var results = await Search(alice, conversation.Id, "50%");

        Assert.Equal(["it costs 50% of nothing"], results.Messages.Select(m => m.Content).ToArray());
    }

    [Fact]
    public async Task An_underscore_is_literal_too()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);

        await Send(alice, conversation.Id, "snake_case naming");
        await Send(bob, conversation.Id, "camelCase naming");

        var results = await Search(alice, conversation.Id, "snake_case");

        Assert.Equal(["snake_case naming"], results.Messages.Select(m => m.Content).ToArray());
    }

    [Fact]
    public async Task A_deleted_message_is_not_findable()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);

        await Send(alice, conversation.Id, "keep this findable");
        var doomed = await Send(alice, conversation.Id, "delete this findable");

        await alice.DeleteAsync(
            $"/api/v1/conversations/{conversation.Id}/messages/{doomed.Id}");

        var results = await Search(bob, conversation.Id, "findable");

        // Both match the term; only the surviving one comes back. Search must not be a way to
        // recover text the server has scrubbed.
        Assert.Equal(["keep this findable"], results.Messages.Select(m => m.Content).ToArray());
    }

    [Fact]
    public async Task A_hidden_message_is_not_findable_by_the_person_who_hid_it()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);

        var message = await Send(alice, conversation.Id, "something you would rather forget");

        await bob.PostAsync($"/api/v1/conversations/{conversation.Id}/messages/{message.Id}/hide", null);

        Assert.Empty((await Search(bob, conversation.Id, "forget")).Messages);

        // But Alice can still find it — she still has her copy. Hiding is not deleting.
        Assert.Equal([message.Id], (await Search(alice, conversation.Id, "forget"))
            .Messages.Select(m => m.Id).ToArray());
    }

    [Fact]
    public async Task Search_is_refused_to_a_non_participant()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var (carol, _) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);
        await Send(alice, conversation.Id, "private to us");

        var response = await carol.GetAsync(
            $"/api/v1/conversations/{conversation.Id}/messages/search?q=private");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Search_is_refused_across_a_block()
    {
        // §8.2: a block makes the conversation "not exist" for both sides, and search is a read
        // of it — otherwise blocking would leave a searchable window into the conversation.
        var (alice, aliceName) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);
        await Send(alice, conversation.Id, "before the block");

        // Blocks are addressed by username, not by id.
        var block = await bob.PostAsJsonAsync($"/api/v1/users/{aliceName}/block", new { });
        Assert.True(
            block.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.Created,
            $"blocking failed: {block.StatusCode}");

        var response = await alice.GetAsync(
            $"/api/v1/conversations/{conversation.Id}/messages/search?q=block");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Results_carry_media_reactions_and_reply_targets_like_the_feed()
    {
        // Search results render as ordinary bubbles, so anything the thread renders from a message
        // has to survive the trip through this endpoint too.
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);

        var parent = await Send(alice, conversation.Id, "parent worth replying to");
        var reply = await Send(bob, conversation.Id, "a reply about needles");

        await alice.PostAsJsonAsync(
            $"/api/v1/conversations/{conversation.Id}/messages/{reply.Id}/reactions",
            new { emoji = "👍" });

        var results = await Search(alice, conversation.Id, "needles");

        var found = Assert.Single(results.Messages);
        Assert.Equal(reply.Id, found.Id);
        Assert.NotNull(found.Reactions);
        Assert.Contains(found.Reactions, reaction => reaction.Count == 1 && reaction.Reacted);
    }
}