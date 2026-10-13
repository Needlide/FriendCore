using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Application.Messaging.Commands.ToggleMessageReaction;
using FriendCore.Domain.Common;
using FriendCore.Domain.Messaging;
using FriendCore.Domain.Users;
using NSubstitute;

namespace FriendCore.UnitTests.Messaging;

/// <summary>
/// Reactions reuse the edit/delete guard order up to "message exists", then diverge: no
/// ownership check, because reacting is a right of every participant rather than a privilege of
/// the sender. What is worth testing here is that divergence — an own-message reaction must
/// succeed where an own-message edit also succeeds but a foreign-message edit does not.
/// </summary>
public class MessageReactionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 9, 0, 0, TimeSpan.Zero);

    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IBlockRepository _blockRepository = Substitute.For<IBlockRepository>();
    private readonly IMessagingRepository _messagingRepository = Substitute.For<IMessagingRepository>();
    private readonly IRealtimeNotifier _realtimeNotifier = Substitute.For<IRealtimeNotifier>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();

    private readonly Ulid _viewerId = Ulid.NewUlid();
    private readonly Ulid _otherId = Ulid.NewUlid();

    private readonly Conversation _conversation;
    private readonly Ulid _conversationId;
    private readonly Message _message;
    private readonly Ulid _messageId;

    public MessageReactionTests()
    {
        _currentUserService.UserId.Returns(_viewerId);
        _dateTimeProvider.UtcNow.Returns(Now);

        _conversation = Conversation.CreateDirect(_viewerId, _otherId, Now).Value;
        _conversationId = _conversation.Id;
        _message = Message.Create(_conversationId, _otherId, "hello", Now).Value;
        _messageId = _message.Id;

        _messagingRepository.GetByIdAsync(_conversationId, Arg.Any<CancellationToken>())
            .Returns(_conversation);
        _messagingRepository.GetMessageAsync(_conversationId, _messageId, Arg.Any<CancellationToken>())
            .Returns(_message);
        _messagingRepository.ReactionExistsAsync(_messageId, _viewerId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);
    }

    private ToggleMessageReactionCommandHandler Handler() =>
        new(_currentUserService, _messagingRepository, _blockRepository, _realtimeNotifier, _dateTimeProvider);

    private Task<ErrorOr<ReactionResponse>> Toggle(string emoji) =>
        Handler().Handle(
            new ToggleMessageReactionCommand(_conversationId, _messageId, emoji), CancellationToken.None);

    // ---- Domain -------------------------------------------------------------------------------

    [Theory]
    [InlineData(ReactionEmoji.ThumbsUp)]
    [InlineData(ReactionEmoji.Heart)]
    [InlineData(ReactionEmoji.Laugh)]
    [InlineData(ReactionEmoji.Sad)]
    [InlineData(ReactionEmoji.Surprised)]
    [InlineData(ReactionEmoji.Thanks)]
    [InlineData(ReactionEmoji.Celebrate)]
    public void Create_AcceptsEveryEmojiOnTheAllowList(string emoji)
    {
        var reaction = MessageReaction.Create(_messageId, _viewerId, emoji, Now);

        Assert.False(reaction.IsError);
        Assert.Equal(emoji, reaction.Value.Emoji);
        Assert.Equal(_messageId, reaction.Value.MessageId);
        Assert.Equal(_viewerId, reaction.Value.UserId);
        Assert.Equal(Now, reaction.Value.CreatedAt);
    }

    [Theory]
    [InlineData("🔥")]        // a perfectly valid emoji that is simply not on the list
    [InlineData("thumbs up")] // words instead of glyphs
    [InlineData("👍🏽")]       // skin-tone modifier smuggled past a naive prefix check
    [InlineData("")]          //
    [InlineData(" ")]         //
    [InlineData(null)]        //
    public void Create_RefusesAnythingOffTheAllowList(string? emoji)
    {
        // The allow-list is a moderation boundary, not a suggestion: reactions are stored,
        // counted and pushed to both participants, so an arbitrary string here would let anyone
        // put a slur into someone else's thread.
        var reaction = MessageReaction.Create(_messageId, _viewerId, emoji!, Now);

        Assert.True(reaction.IsError);
        Assert.Equal("Messaging.ReactionNotAllowed", reaction.FirstError.Code);
    }

    [Fact]
    public void IsAllowed_IsOrdinal_NotCultureAware()
    {
        // Guards the comparison itself: a culture-aware Contains would accept lookalikes that
        // render as something else entirely.
        Assert.False(ReactionEmoji.IsAllowed("thumbsup"));
        Assert.False(ReactionEmoji.IsAllowed("❤"));        // without the variation selector
        Assert.True(ReactionEmoji.IsAllowed(ReactionEmoji.Heart));
    }

    // ---- Handler ------------------------------------------------------------------------------

    [Fact]
    public async Task Toggle_ReturnsUnauthorized_WhenNoCurrentUser()
    {
        _currentUserService.UserId.Returns((Ulid?)null);

        var result = await Toggle(ReactionEmoji.ThumbsUp);

        Assert.Equal("Common.Unauthorized", result.FirstError.Code);
    }

    [Fact]
    public async Task Toggle_ReturnsConversationNotFound_WhenUserIsNotAParticipant()
    {
        _currentUserService.UserId.Returns(Ulid.NewUlid());

        var result = await Toggle(ReactionEmoji.ThumbsUp);

        Assert.Equal("Messaging.ConversationNotFound", result.FirstError.Code);
    }

    [Fact]
    public async Task Toggle_ReturnsConversationNotFound_WhenBlocked()
    {
        // §8.2: a blocked thread is unreactable, and indistinguishable from one that never existed.
        _blockRepository.ExistsBetweenAsync(_viewerId, _otherId, Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await Toggle(ReactionEmoji.ThumbsUp);

        Assert.Equal("Messaging.ConversationNotFound", result.FirstError.Code);
    }

    [Fact]
    public async Task Toggle_ReturnsMessageNotFound_WhenTheMessageIsGone()
    {
        // The global soft-delete filter means a deleted message arrives here as null, so a
        // reaction cannot outlive or resurrect the thing it points at.
        _messagingRepository.GetMessageAsync(_conversationId, _messageId, Arg.Any<CancellationToken>())
            .Returns((Message?)null);

        var result = await Toggle(ReactionEmoji.ThumbsUp);

        Assert.Equal("Messaging.MessageNotFound", result.FirstError.Code);
    }

    [Fact]
    public async Task Toggle_AllowsReactingToTheOtherParticipantsMessage()
    {
        // The deliberate divergence from edit/delete: no ownership check, because anyone in the
        // thread may react to anything in it. NotMessageSender would be the wrong error here.
        _messagingRepository.GetReactionTallyAsync(_messageId, ReactionEmoji.Heart, _viewerId, Arg.Any<CancellationToken>())
            .Returns(new ReactionTally(ReactionEmoji.Heart, 1, true));

        var result = await Toggle(ReactionEmoji.Heart);

        Assert.False(result.IsError);
        Assert.True(result.Value.Reacted);
    }

    [Fact]
    public async Task Toggle_AllowsReactingToYourOwnMessage()
    {
        var mine = Message.Create(_conversationId, _viewerId, "my own words", Now).Value;
        _messagingRepository.GetMessageAsync(_conversationId, mine.Id, Arg.Any<CancellationToken>())
            .Returns(mine);
        _messagingRepository.ReactionExistsAsync(mine.Id, _viewerId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await Handler().Handle(
            new ToggleMessageReactionCommand(_conversationId, mine.Id, ReactionEmoji.ThumbsUp),
            CancellationToken.None);

        Assert.False(result.IsError);
        await _messagingRepository.Received(1).AddReactionAsync(
            Arg.Is<MessageReaction>(r => r.MessageId == mine.Id && r.UserId == _viewerId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Toggle_RefusesAnEmojiOffTheAllowList()
    {
        var result = await Toggle("🔥");

        Assert.Equal("Messaging.ReactionNotAllowed", result.FirstError.Code);
        await _messagingRepository.DidNotReceiveWithAnyArgs().AddReactionAsync(default!);
    }

    [Fact]
    public async Task Toggle_AddsWhenTheViewerDoesNotHoldItYet()
    {
        _messagingRepository.ReactionExistsAsync(_messageId, _viewerId, ReactionEmoji.Laugh, Arg.Any<CancellationToken>())
            .Returns(false);
        _messagingRepository.GetReactionTallyAsync(_messageId, ReactionEmoji.Laugh, _viewerId, Arg.Any<CancellationToken>())
            .Returns(new ReactionTally(ReactionEmoji.Laugh, 1, true));

        var result = await Toggle(ReactionEmoji.Laugh);

        Assert.False(result.IsError);
        Assert.Equal(1, result.Value.Count);
        Assert.True(result.Value.Reacted);
        await _messagingRepository.Received(1).AddReactionAsync(
            Arg.Is<MessageReaction>(r => r.Emoji == ReactionEmoji.Laugh && r.CreatedAt == Now),
            Arg.Any<CancellationToken>());
        await _messagingRepository.DidNotReceiveWithAnyArgs().RemoveReactionAsync(default!, default!, default!, default!);
    }

    [Fact]
    public async Task Toggle_RemovesWhenTheViewerAlreadyHoldsIt()
    {
        // The undo path: tapping a highlighted chip again takes the reaction back.
        _messagingRepository.ReactionExistsAsync(_messageId, _viewerId, ReactionEmoji.Laugh, Arg.Any<CancellationToken>())
            .Returns(true);
        _messagingRepository.RemoveReactionAsync(_messageId, _viewerId, ReactionEmoji.Laugh, Arg.Any<CancellationToken>())
            .Returns(true);
        _messagingRepository.GetReactionTallyAsync(_messageId, ReactionEmoji.Laugh, _viewerId, Arg.Any<CancellationToken>())
            .Returns(new ReactionTally(ReactionEmoji.Laugh, 1, false));

        var result = await Toggle(ReactionEmoji.Laugh);

        Assert.False(result.IsError);
        Assert.Equal(1, result.Value.Count);
        Assert.False(result.Value.Reacted);
        await _messagingRepository.Received(1).RemoveReactionAsync(
            _messageId, _viewerId, ReactionEmoji.Laugh, Arg.Any<CancellationToken>());
        await _messagingRepository.DidNotReceiveWithAnyArgs().AddReactionAsync(default!);
    }

    [Fact]
    public async Task Toggle_ReportsAZeroCount_WhenTheLastReactionGoesAway()
    {
        // Nobody holds it any more: the client is told 0 and drops the chip, mirroring how a
        // deleted message simply vanishes rather than leaving a tombstone.
        _messagingRepository.ReactionExistsAsync(_messageId, _viewerId, ReactionEmoji.Celebrate, Arg.Any<CancellationToken>())
            .Returns(true);
        _messagingRepository.GetReactionTallyAsync(_messageId, ReactionEmoji.Celebrate, _viewerId, Arg.Any<CancellationToken>())
            .Returns((ReactionTally?)null);

        var result = await Toggle(ReactionEmoji.Celebrate);

        Assert.False(result.IsError);
        Assert.Equal(0, result.Value.Count);
        Assert.False(result.Value.Reacted);
    }

    [Fact]
    public async Task Toggle_PushesToBothParticipants()
    {
        // Same audience as an edit: the recipient's open thread has to move the chip, and the
        // tapper's other devices would otherwise stay stale — MessageReceived never reaches them.
        _messagingRepository.GetReactionTallyAsync(_messageId, ReactionEmoji.ThumbsUp, _viewerId, Arg.Any<CancellationToken>())
            .Returns(new ReactionTally(ReactionEmoji.ThumbsUp, 2, true));

        await Toggle(ReactionEmoji.ThumbsUp);

        await _realtimeNotifier.Received(1).MessageReactionChangedAsync(
            _conversationId,
            Arg.Is<IEnumerable<Ulid>>(ids => ids.OrderBy(id => id).SequenceEqual(
                new[] { _viewerId, _otherId }.OrderBy(id => id))),
            _messageId,
            Arg.Is<ReactionResponse>(r => r.Emoji == ReactionEmoji.ThumbsUp && r.Count == 2 && r.Reacted),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Toggle_PushesANullReaction_WhenTheChipShouldDisappear()
    {
        _messagingRepository.ReactionExistsAsync(_messageId, _viewerId, ReactionEmoji.Sad, Arg.Any<CancellationToken>())
            .Returns(true);
        _messagingRepository.GetReactionTallyAsync(_messageId, ReactionEmoji.Sad, _viewerId, Arg.Any<CancellationToken>())
            .Returns((ReactionTally?)null);

        await Toggle(ReactionEmoji.Sad);

        await _realtimeNotifier.Received(1).MessageReactionChangedAsync(
            _conversationId, Arg.Any<IEnumerable<Ulid>>(), _messageId,
            (ReactionResponse?)null,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Toggle_RaisesNoNotification()
    {
        // Unlike a like, a reaction is an acknowledgement rather than a message: a bell entry per
        // tap would bury the NewMessage notifications the thread actually depends on.
        _messagingRepository.GetReactionTallyAsync(_messageId, Arg.Any<string>(), Arg.Any<Ulid>(), Arg.Any<CancellationToken>())
            .Returns(new ReactionTally(ReactionEmoji.ThumbsUp, 1, true));

        await Toggle(ReactionEmoji.ThumbsUp);

        await _realtimeNotifier.DidNotReceiveWithAnyArgs()
            .NotificationReceivedAsync(default!, default!, default!);
    }
}