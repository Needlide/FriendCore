using System.Net;
using System.Net.Http.Json;
using FriendCore.Application.Common.Models;
using FriendCore.Infrastructure.Persistence;
using FriendCore.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FriendCore.IntegrationTests.Messaging;

/// <summary>
/// Emoji reactions on a message, end to end against Postgres. The load-bearing cases are the
/// ones a mocked repository cannot prove: that the per-page tally query translates to SQL
/// (the Ulid key conversion has bitten this module before), that both participants see the
/// chips, and that a soft-deleted message takes its reactions with it.
/// </summary>
public class MessageReactionFlowTests(FriendCoreApiFactory factory) : IClassFixture<FriendCoreApiFactory>
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

    private static Task<HttpResponseMessage> Toggle(
        HttpClient client, string conversationId, string messageId, string emoji) =>
        client.PostAsJsonAsync($"/api/v1/conversations/{conversationId}/messages/{messageId}/reactions", new { emoji });

    private static async Task<ReactionResponse> ToggleOk(
        HttpClient client, string conversationId, string messageId, string emoji)
    {
        var response = await Toggle(client, conversationId, messageId, emoji);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ReactionResponse>())!;
    }

    /// <summary>The chips the given user sees on one message, as the message feed renders them.</summary>
    private static async Task<IReadOnlyList<ReactionResponse>> ReactionsOf(
        HttpClient client, string conversationId, string messageId)
    {
        var feed = await client.GetFromJsonAsync<MessageFeedResponse>(
            $"/api/v1/conversations/{conversationId}/messages");
        var message = feed!.Messages.Single(m => m.Id == messageId);

        // A message with no reactions must serialize as [], never null — the client would
        // otherwise guard every read of this field.
        Assert.NotNull(message.Reactions);
        return message.Reactions;
    }

    [Fact]
    public async Task Reacting_ShowsTheChip_ToBothParticipants()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);
        var message = await SendMessage(alice, conversation.Id, "the cat sat");

        var result = await ToggleOk(bob, conversation.Id, message.Id, "👍");
        Assert.Equal("👍", result.Emoji);
        Assert.Equal(1, result.Count);
        Assert.True(result.Reacted);

        var forBob = await ReactionsOf(bob, conversation.Id, message.Id);
        Assert.Single(forBob);
        Assert.True(forBob.Single().Reacted);

        // The sender's own open thread shows the chip too, not just the responder's.
        var forAlice = await ReactionsOf(alice, conversation.Id, message.Id);
        Assert.Single(forAlice);
        Assert.Equal(1, forAlice.Single().Count);
        // ...but marked as not-mine, because Alice is not the reactor.
        Assert.False(forAlice.Single().Reacted);
    }

    [Fact]
    public async Task TogglingTwice_TakesTheReactionBack()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);
        var message = await SendMessage(alice, conversation.Id, "hello");

        await ToggleOk(bob, conversation.Id, message.Id, "❤️");

        var removed = await ToggleOk(bob, conversation.Id, message.Id, "❤️");
        Assert.Equal(0, removed.Count);
        Assert.False(removed.Reacted);

        // Gone for both sides — a released reaction leaves no tombstone behind.
        Assert.Empty(await ReactionsOf(bob, conversation.Id, message.Id));
        Assert.Empty(await ReactionsOf(alice, conversation.Id, message.Id));
    }

    [Fact]
    public async Task ReactionsFromBothParticipants_CountTogether_AndFlagWhoIsMine()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);
        var message = await SendMessage(alice, conversation.Id, "hello");

        await ToggleOk(alice, conversation.Id, message.Id, "😂");
        await ToggleOk(bob, conversation.Id, message.Id, "😂");

        var asAlice = (await ReactionsOf(alice, conversation.Id, message.Id)).Single();
        Assert.Equal(2, asAlice.Count);
        Assert.True(asAlice.Reacted);

        var asBob = (await ReactionsOf(bob, conversation.Id, message.Id)).Single();
        Assert.Equal(2, asBob.Count);
        Assert.True(asBob.Reacted);

        // One participant leaving drops the count but leaves the other participant's chip.
        await ToggleOk(bob, conversation.Id, message.Id, "😂");
        var afterAlice = (await ReactionsOf(alice, conversation.Id, message.Id)).Single();
        Assert.Equal(1, afterAlice.Count);
        Assert.True(afterAlice.Reacted);
    }

    [Fact]
    public async Task DifferentEmoji_CountSeparately_AndOneUserMayHoldBoth()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);
        var message = await SendMessage(alice, conversation.Id, "congrats");

        await ToggleOk(bob, conversation.Id, message.Id, "🎉");
        await ToggleOk(alice, conversation.Id, message.Id, "🎉");
        await ToggleOk(alice, conversation.Id, message.Id, "👍");

        // The composite key is (message, user, emoji): several reactions per user, never two
        // of the same one.
        var asAlice = await ReactionsOf(alice, conversation.Id, message.Id);
        Assert.Equal(2, asAlice.Count);
        Assert.All(asAlice, chip => Assert.True(chip.Reacted));
        Assert.Equal(2, asAlice.Single(chip => chip.Emoji == "🎉").Count);
        Assert.Equal(1, asAlice.Single(chip => chip.Emoji == "👍").Count);

        // Bob sees both chips too — the other participant's reaction is not hidden from him —
        // but only the one he actually holds is flagged as his.
        var asBob = await ReactionsOf(bob, conversation.Id, message.Id);
        Assert.Equal(2, asBob.Count);
        Assert.True(asBob.Single(chip => chip.Emoji == "🎉").Reacted);
        Assert.Equal(2, asBob.Single(chip => chip.Emoji == "🎉").Count);
        Assert.False(asBob.Single(chip => chip.Emoji == "👍").Reacted);
        Assert.Equal(1, asBob.Single(chip => chip.Emoji == "👍").Count);
    }

    [Fact]
    public async Task ChipsAreOrderedByCount_ThenPickerOrder()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);
        var message = await SendMessage(alice, conversation.Id, "hello");

        // One each first, so both tie at a count of 1 and the tiebreak is what decides order.
        await ToggleOk(alice, conversation.Id, message.Id, "🎉");
        await ToggleOk(bob, conversation.Id, message.Id, "🎉");
        await ToggleOk(alice, conversation.Id, message.Id, "👍");

        var chips = await ReactionsOf(alice, conversation.Id, message.Id);

        // Most-reacted first...
        Assert.Equal("🎉", chips[0].Emoji);
        // ...then picker order, so chips do not reshuffle between two reads on a tie.
        Assert.Equal("👍", chips[1].Emoji);
    }

    [Fact]
    public async Task AnyoneInTheThread_MayReactToAnyMessage_IncludingYourOwn()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);
        var message = await SendMessage(alice, conversation.Id, "my own words");

        // No ownership rule on reactions — only edit and delete are the sender's alone.
        var result = await ToggleOk(bob, conversation.Id, message.Id, "🙏");
        Assert.Equal(1, result.Count);

        var own = await SendMessage(bob, conversation.Id, "and mine");
        var onOwnMessage = await ToggleOk(alice, conversation.Id, own.Id, "🙏");
        Assert.Equal(1, onOwnMessage.Count);
    }

    [Fact]
    public async Task EditingAMessage_KeepsItsReactions()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);
        var message = await SendMessage(alice, conversation.Id, "teh cat sat");

        await ToggleOk(bob, conversation.Id, message.Id, "😂");

        // version 0: the message has never been edited, which is what the client would hold.
        var edit = await alice.PatchAsJsonAsync(
            $"/api/v1/conversations/{conversation.Id}/messages/{message.Id}",
            new { content = "the cat sat", version = 0 });
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);

        // The chips must survive an edit, and survive in the pushed copy too: the recipient's
        // thread would otherwise lose every chip on each saved edit.
        var edited = await edit.Content.ReadFromJsonAsync<MessageResponse>();
        Assert.Equal("the cat sat", edited!.Content);
        Assert.NotNull(edited.Reactions);
        Assert.Single(edited.Reactions);
        Assert.Equal("😂", edited.Reactions.Single().Emoji);

        Assert.Single(await ReactionsOf(bob, conversation.Id, message.Id));
    }

    [Fact]
    public async Task DeletingAMessage_TakesItsReactionsWithIt()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);
        var message = await SendMessage(alice, conversation.Id, "delete me");

        await ToggleOk(alice, conversation.Id, message.Id, "👍");
        await ToggleOk(bob, conversation.Id, message.Id, "👍");

        var deleted = await alice.DeleteAsync(
            $"/api/v1/conversations/{conversation.Id}/messages/{message.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        // Nothing renders a chip for a message that is gone...
        var feed = await alice.GetFromJsonAsync<MessageFeedResponse>(
            $"/api/v1/conversations/{conversation.Id}/messages");
        Assert.DoesNotContain(feed!.Messages, m => m.Id == message.Id);

        // ...and nothing is left behind on the row. Reactions have no FK to messages (the same
        // deliberate choice the rest of the messaging tables make), so this cleanup is the only
        // thing preventing orphans.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.MessageReactions.AnyAsync(r => r.MessageId == Ulid.Parse(message.Id)));
    }

    [Fact]
    public async Task ReactionsOnADeletedMessage_AreRefused()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);
        var message = await SendMessage(alice, conversation.Id, "goodbye");
        await alice.DeleteAsync($"/api/v1/conversations/{conversation.Id}/messages/{message.Id}");

        var response = await Toggle(bob, conversation.Id, message.Id, "👍");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AnEmojiOffTheAllowList_IsRejected()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);
        var message = await SendMessage(alice, conversation.Id, "hello");

        // A perfectly valid emoji that is simply not on the list, plus some free text. Reactions
        // are stored, counted and pushed to both participants, so an arbitrary string here would
        // let anyone put a slur into someone else's thread.
        foreach (var emoji in new[] { "🔥", "thumbs up", "👍🏽", "" })
        {
            var response = await Toggle(bob, conversation.Id, message.Id, emoji);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        Assert.Empty(await ReactionsOf(bob, conversation.Id, message.Id));
    }

    [Fact]
    public async Task AnOutsider_CannotReact_AndLearnsNothing()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var (_, carolName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);
        var message = await SendMessage(alice, conversation.Id, "private");

        var (carol, _) = await RegisterClient();
        var response = await Toggle(carol, conversation.Id, message.Id, "👍");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AMalformedId_Is404_Not400()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);

        // Same anti-enumeration rule as every other messaging action: probing cannot tell a
        // malformed id from one that never existed.
        foreach (var (conversationId, messageId) in new[]
                 {
                     ("not-a-ulid", "also-not"),
                     (conversation.Id, "not-a-ulid"),
                     ("not-a-ulid", "not-a-ulid")
                 })
        {
            var response = await Toggle(bob, conversationId, messageId, "👍");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    [Fact]
    public async Task AMessageIdFromAnotherConversation_Is404()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var (carol, carolName) = await RegisterClient();

        var aliceToBob = await StartConversation(alice, bobName);
        var aliceToCarol = await StartConversation(alice, carolName);

        // A message in one thread, referenced through the other: never a cross-thread write.
        var message = await SendMessage(alice, aliceToBob.Id, "for bob only");

        var response = await Toggle(carol, aliceToCarol.Id, message.Id, "👍");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Reacting_RequiresAuthentication()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);
        var message = await SendMessage(alice, conversation.Id, "hello");

        // From the factory: a bare HttpClient with a copied BaseAddress would dial a real
        // socket instead of the TestServer, and the test would fail for the wrong reason.
        using var anonymous = factory.CreateClient();

        var response = await Toggle(anonymous, conversation.Id, message.Id, "👍");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AMissingEmoji_Is400_RatherThanASilentNoOp()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);
        var message = await SendMessage(alice, conversation.Id, "hello");

        // Distinct from the ids, which 404 on purpose: a body with no emoji is a malformed
        // request from a real client, not an attempt to probe.
        var response = await bob.PostAsJsonAsync(
            $"/api/v1/conversations/{conversation.Id}/messages/{message.Id}/reactions",
            new { });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}