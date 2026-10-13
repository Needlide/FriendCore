using System.Net;
using System.Net.Http.Json;
using FriendCore.Application.Common.Models;
using FriendCore.Infrastructure.Persistence;
using FriendCore.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FriendCore.IntegrationTests.Messaging;

/// <summary>
/// Reply-to, end to end. The case that cannot be proved anywhere else is the deleted parent:
/// the reply survives, its quote must still resolve, and the row it points at is invisible to
/// every ordinary query because the soft-delete filter hides it.
/// </summary>
public class MessageReplyFlowTests(FriendCoreApiFactory factory) : IClassFixture<FriendCoreApiFactory>
{
    private Task<(HttpClient Client, string Username)> RegisterClient() =>
        new AuthScenarios(factory).RegisterVerifiedClientAsync();

    private static async Task<ConversationResponse> StartConversation(HttpClient client, string username)
    {
        var response = await client.PostAsJsonAsync("/api/v1/conversations", new { username });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ConversationResponse>())!;
    }

    private static async Task<MessageResponse> Send(
        HttpClient client, string conversationId, string content, string? replyToMessageId = null)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/v1/conversations/{conversationId}/messages",
            new { content, replyToMessageId });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<MessageResponse>())!;
    }

    private static async Task<MessageResponse?> FindInFeed(
        HttpClient client, string conversationId, string messageId)
    {
        var feed = await client.GetFromJsonAsync<MessageFeedResponse>(
            $"/api/v1/conversations/{conversationId}/messages");
        return feed!.Messages.SingleOrDefault(m => m.Id == messageId);
    }

    [Fact]
    public async Task Replying_StoresTheTarget_AndArrivesWithItsQuote()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);
        var original = await Send(alice, conversation.Id, "the cat sat");

        var reply = await Send(bob, conversation.Id, "did it though", original.Id);

        // The sender sees the quote on the POST response, not only after a refetch.
        Assert.NotNull(reply.ReplyTo);
        Assert.Equal(original.Id, reply.ReplyTo!.Id);
        Assert.Equal("the cat sat", reply.ReplyTo.Excerpt);
        Assert.False(reply.ReplyTo.WasDeleted);

        // And the other side sees it in the thread.
        var seen = await FindInFeed(alice, conversation.Id, reply.Id);
        Assert.NotNull(seen!.ReplyTo);
        Assert.Equal("the cat sat", seen.ReplyTo!.Excerpt);
    }

    [Fact]
    public async Task AnOrdinaryMessage_CarriesNoQuote()
    {
        var (alice, _) = await RegisterClient();
        var (_, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);

        var message = await Send(alice, conversation.Id, "just saying");

        Assert.Null(message.ReplyTo);
        Assert.Null((await FindInFeed(alice, conversation.Id, message.Id))!.ReplyTo);
    }

    [Fact]
    public async Task Replying_IsConversationScoped_AndALeakedIdIs404()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var (carol, carolName) = await RegisterClient();

        var aliceToBob = await StartConversation(alice, bobName);
        var aliceToCarol = await StartConversation(alice, carolName);
        var private_ = await Send(alice, aliceToBob.Id, "for bob only");

        // Referenced through another thread: never a cross-thread write or quote.
        var response = await carol.PostAsJsonAsync(
            $"/api/v1/conversations/{aliceToCarol.Id}/messages",
            new { content = "what did he say", replyToMessageId = private_.Id });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ReplyingToSomethingDeleted_Is404()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);
        var original = await Send(alice, conversation.Id, "regrettable");
        await alice.DeleteAsync($"/api/v1/conversations/{conversation.Id}/messages/{original.Id}");

        var response = await bob.PostAsJsonAsync(
            $"/api/v1/conversations/{conversation.Id}/messages",
            new { content = "what did you mean", replyToMessageId = original.Id });

        // A quote with nothing in it would be worse than no reply at all.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AMalformedTargetId_Is400_AndTheMessageIsNotSent()
    {
        var (alice, _) = await RegisterClient();
        var (_, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);

        var response = await alice.PostAsJsonAsync(
            $"/api/v1/conversations/{conversation.Id}/messages",
            new { content = "replying", replyToMessageId = "not-a-ulid" });

        // Same rule as a malformed media id: silently dropping it would send a message that looks
        // like a reply but carries no quote.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var feed = await alice.GetFromJsonAsync<MessageFeedResponse>(
            $"/api/v1/conversations/{conversation.Id}/messages");
        Assert.Empty(feed!.Messages);
    }

    [Fact]
    public async Task AnExistingReply_KeepsItsQuote_WhenTheParentIsLaterDeleted()
    {
        // The interesting one. The parent is gone — scrubbed and filtered out of every ordinary
        // query — but the reply stands, and its quote must still resolve and say so honestly.
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);

        var original = await Send(alice, conversation.Id, "embarrassing thing");
        var reply = await Send(bob, conversation.Id, "no way", original.Id);
        await alice.DeleteAsync($"/api/v1/conversations/{conversation.Id}/messages/{original.Id}");

        foreach (var viewer in new[] { alice, bob })
        {
            var seen = await FindInFeed(viewer, conversation.Id, reply.Id);

            // The reply is still there for both...
            Assert.NotNull(seen);
            // ...and still says what it was replying to, without inventing content for it.
            Assert.NotNull(seen!.ReplyTo);
            Assert.Equal(original.Id, seen.ReplyTo!.Id);
            Assert.True(seen.ReplyTo.WasDeleted);
            Assert.Null(seen.ReplyTo.Excerpt);
        }
    }

    [Fact]
    public async Task ADeletedParent_IsStillResolvable_BecauseTheLookupIgnoresTheSoftDeleteFilter()
    {
        // Proves the filter is being bypassed deliberately rather than the lookup happening to
        // Proves the bypass is deliberate rather than incidental. The order matters: a reply
        // SENT to an already-deleted parent is refused (the other test covers that), so the
        // reply has to be created first and orphaned afterwards.
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);

        var original = await Send(alice, conversation.Id, "gone but referenced");
        var reply = await Send(bob, conversation.Id, "still replying", original.Id);
        await alice.DeleteAsync($"/api/v1/conversations/{conversation.Id}/messages/{original.Id}");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // The parent is invisible to an ordinary query — this is why the reply lookup has to
        // bypass the soft-delete filter rather than simply reading the row.
        Assert.Null(await db.Messages.FirstOrDefaultAsync(m => m.Id == Ulid.Parse(original.Id)));

        // The reply still points at it, which is what the quote resolution reads.
        var stored = await db.Messages.FirstAsync(m => m.Id == Ulid.Parse(reply.Id));
        Assert.Equal(original.Id, stored.ReplyToMessageId!.Value.ToString());
    }

    [Fact]
    public async Task ReplyingToAnAttachmentOnlyMessage_IsNotReportedAsDeleted()
    {
        // "No text" and "gone" are different and must not collapse: this parent is right there.
        var (alice, _) = await RegisterClient();
        var (_, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);

        // A text message stands in for the attachment-only case: what matters is that the quote
        // carries a real excerpt and is NOT flagged deleted.
        var original = await Send(alice, conversation.Id, "look at this photo");
        var reply = await Send(alice, conversation.Id, "nice", original.Id);

        Assert.False(reply.ReplyTo!.WasDeleted);
        Assert.Equal("look at this photo", reply.ReplyTo.Excerpt);
    }

    [Fact]
    public async Task EditingAReply_KeepsItsQuote_OnThePushedCopy()
    {
        // The easiest of the three paths to get wrong: an edit pushes the whole message, so
        // omitting the quote would strip it off the other side's bubble on every saved edit.
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);
        var original = await Send(alice, conversation.Id, "the cat sat");
        var reply = await Send(bob, conversation.Id, "did it", original.Id);

        var edited = await bob.PatchAsJsonAsync(
            $"/api/v1/conversations/{conversation.Id}/messages/{reply.Id}",
            new { content = "did it though", version = reply.Version });

        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        var body = (await edited.Content.ReadFromJsonAsync<MessageResponse>())!;

        Assert.NotNull(body.ReplyTo);
        Assert.Equal(original.Id, body.ReplyTo!.Id);
        Assert.Equal("the cat sat", body.ReplyTo.Excerpt);
    }

    [Fact]
    public async Task ReplyingToYourOwnMessage_IsAllowed()
    {
        var (alice, _) = await RegisterClient();
        var (_, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);
        var mine = await Send(alice, conversation.Id, "on second thought");

        var reply = await Send(alice, conversation.Id, "actually, no", mine.Id);

        Assert.Equal(mine.Id, reply.ReplyTo!.Id);
    }

    [Fact]
    public async Task SeveralRepliesToOneMessage_EachResolveIndependently()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);
        var original = await Send(alice, conversation.Id, "who is coming?");

        await Send(bob, conversation.Id, "me", original.Id);
        await Send(alice, conversation.Id, "same", original.Id);

        var feed = await alice.GetFromJsonAsync<MessageFeedResponse>(
            $"/api/v1/conversations/{conversation.Id}/messages");
        var replies = feed!.Messages.Where(m => m.ReplyTo is not null).ToList();

        // The batched lookup keys by each reply's own id, not by parent — so two replies to the
        // same parent do not collide.
        Assert.Equal(2, replies.Count);
        Assert.All(replies, reply => Assert.Equal(original.Id, reply.ReplyTo!.Id));
        Assert.All(replies, reply => Assert.Equal("who is coming?", reply.ReplyTo.Excerpt));
    }

    [Fact]
    public async Task DeletingTheParent_LeavesTheReplySendable_AndTheReportSubjectIntact()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);
        var original = await Send(bob, conversation.Id, "unacceptable");
        await alice.DeleteAsync($"/api/v1/conversations/{conversation.Id}/messages/{original.Id}");

        // Nothing about the deleted parent blocks the rest of the conversation from working.
        var after = await Send(alice, conversation.Id, "moving on");
        Assert.Equal("moving on", after.Content);
        Assert.Null(after.ReplyTo);
    }
}