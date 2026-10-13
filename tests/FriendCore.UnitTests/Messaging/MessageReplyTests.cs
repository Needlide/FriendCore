using FriendCore.Application.Common;
using FriendCore.Domain.Messaging;
using NSubstitute;

namespace FriendCore.UnitTests.Messaging;

/// <summary>
/// Reply-to. The load-bearing cases are the ones a naive implementation gets wrong in a way no
/// ordinary round trip would reveal: a reply whose parent has since been deleted, and the
/// difference between "attached with no text" and "gone".
/// </summary>
public class MessageReplyTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);

    private readonly IMessagingRepository _messagingRepository = Substitute.For<IMessagingRepository>();
    private readonly ReplyReferenceFactory _factory;

    private readonly Ulid _conversationId = Ulid.NewUlid();
    private readonly Ulid _authorId = Ulid.NewUlid();

    public MessageReplyTests()
    {
        _factory = new ReplyReferenceFactory(_messagingRepository);
    }

    private Message Create(string? content, Ulid? replyTo = null) =>
        Message.Create(_conversationId, _authorId, content, Now, null, replyTo).Value;

    private void Parents(params Message[] parents) =>
        _messagingRepository
            .GetByIdsIncludingDeletedAsync(Arg.Any<IReadOnlyCollection<Ulid>>(), Arg.Any<CancellationToken>())
            .Returns(parents.ToDictionary(parent => parent.Id));

    // ---- domain ------------------------------------------------------------------------------

    [Fact]
    public void Create_StoresTheReplyTarget()
    {
        var parent = Create("the original");

        var reply = Create("agreeing", parent.Id);

        Assert.Equal(parent.Id, reply.ReplyToMessageId);
    }

    [Fact]
    public void Create_LeavesAnOrdinaryMessageWithNoTarget()
    {
        Assert.Null(Create("just a message").ReplyToMessageId);
    }

    [Fact]
    public void Create_AllowsReplyingToYourOwnMessage()
    {
        // Threading a thought by hand is normal, and refusing self-replies would be a rule with
        // no purpose behind it.
        var mine = Create("on second thought");

        var reply = Create("actually, no", mine.Id);

        Assert.Equal(mine.Id, reply.ReplyToMessageId);
    }

    [Fact]
    public void Create_StillAllowsAnAttachmentOnlyReplyWithNoText()
    {
        var parent = Create("here is the thing");
        var mediaId = Ulid.NewUlid();

        var reply = Message.Create(_conversationId, _authorId, null, Now, mediaId, parent.Id).Value;

        Assert.Equal(parent.Id, reply.ReplyToMessageId);
        Assert.Null(reply.Content);
    }

    [Fact]
    public void Edit_DoesNotDisturbTheReplyTarget()
    {
        var parent = Create("the original");
        var reply = Create("agreeing", parent.Id);

        reply.Edit("still agreeing", Now.AddMinutes(1));

        // Rewording a reply must not silently turn it into an ordinary message.
        Assert.Equal(parent.Id, reply.ReplyToMessageId);
    }

    // ---- the quoted reference ------------------------------------------------------------------

    [Fact]
    public void Reference_CarriesAShortExcerpt()
    {
        var parent = Create("a perfectly reasonable message");

        var reference = MessageReference.From(parent);

        Assert.Equal(parent.Id, reference.Id);
        Assert.Equal(_authorId, reference.SenderId);
        Assert.Equal("a perfectly reasonable message", reference.Excerpt);
        Assert.False(reference.WasDeleted);
    }

    [Fact]
    public void Reference_TruncatesALongMessage()
    {
        var parent = Create(new string('x', MessageReference.MaxExcerptLength + 50));

        var reference = MessageReference.From(parent);

        // A quote is a reference, not a copy: rendering the whole message would make a reply to a
        // long message mostly a quote of that message.
        Assert.NotNull(reference.Excerpt);
        Assert.True(reference.Excerpt!.Length <= MessageReference.MaxExcerptLength + 1);
        Assert.EndsWith("…", reference.Excerpt);
    }

    [Fact]
    public void Reference_MarksADeletedParentAsDeleted_WithNoExcerpt()
    {
        var parent = Create("something they later erased");
        parent.MarkDeleted(Now.AddMinutes(5));

        var reference = MessageReference.From(parent);

        // The two null-excerpt cases are different and must be told apart: this one means the
        // quote's subject is gone, the other means it never had words.
        Assert.True(reference.WasDeleted);
        Assert.Null(reference.Excerpt);
    }

    [Fact]
    public void Reference_DoesNotCallAnAttachmentOnlyParentDeleted()
    {
        var mediaId = Ulid.NewUlid();
        var parent = Message.Create(_conversationId, _authorId, null, Now, mediaId).Value;

        var reference = MessageReference.From(parent);

        // No text, but perfectly much there — rendering this as "deleted" would tell the reader
        // something false about a conversation they can see.
        Assert.Null(reference.Excerpt);
        Assert.False(reference.WasDeleted);
    }

    [Fact]
    public async Task Factory_ResolvesEveryReplyOnAPage_InOneLookup()
    {
        var parent = Create("the original");
        var otherParent = Create("a different one");
        var reply = Create("agreeing", parent.Id);
        var secondReply = Create("disagreeing", otherParent.Id);
        Parents(parent, otherParent);

        var resolved = await _factory.ForMessagesAsync(
            [reply, secondReply, Create("not a reply")], CancellationToken.None);

        Assert.Equal(2, resolved.Count);
        Assert.Equal(parent.Id.ToString(), resolved[reply.Id].Id);
        Assert.Equal(otherParent.Id.ToString(), resolved[secondReply.Id].Id);

        // One call for the page, not one per reply — this is the whole reason the factory exists.
        await _messagingRepository.Received(1)
            .GetByIdsIncludingDeletedAsync(Arg.Any<IReadOnlyCollection<Ulid>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Factory_MakesNoLookup_WhenNothingReplies()
    {
        var resolved = await _factory.ForMessagesAsync(
            [Create("plain"), Create("also plain")], CancellationToken.None);

        Assert.Empty(resolved);
        // Not even a wasted round trip: most messages reply to nothing.
        await _messagingRepository.DidNotReceiveWithAnyArgs()
            .GetByIdsIncludingDeletedAsync(default!, default!);
    }

    [Fact]
    public async Task Factory_OmitsTheQuote_WhenTheParentRowCannotBeLoaded()
    {
        var reply = Create("agreeing", Ulid.NewUlid());
        // No parents registered — the lookup comes back empty.
        Parents();

        var resolved = await _factory.ForMessagesAsync([reply], CancellationToken.None);

        // Fabricating a quote would be worse than omitting one: the client would show something
        // that is not what the server actually holds.
        Assert.Empty(resolved);
    }

    [Fact]
    public async Task Factory_ResolvesASingleReply()
    {
        var parent = Create("the original");
        var reply = Create("agreeing", parent.Id);
        Parents(parent);

        var reference = await _factory.ForMessageAsync(reply, CancellationToken.None);

        Assert.NotNull(reference);
        Assert.Equal(parent.Id.ToString(), reference!.Id);
    }

    [Fact]
    public async Task Factory_ReturnsNullForAMessageThatRepliesToNothing()
    {
        Assert.Null(await _factory.ForMessageAsync(Create("plain"), CancellationToken.None));
        await _messagingRepository.DidNotReceiveWithAnyArgs()
            .GetByIdsIncludingDeletedAsync(default!, default!);
    }
}