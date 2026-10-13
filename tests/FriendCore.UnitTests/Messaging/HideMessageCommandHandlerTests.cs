using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Messaging.Commands.HideMessage;
using FriendCore.Domain.Common;
using FriendCore.Domain.Messaging;
using FriendCore.Domain.Users;
using MediatR;
using NSubstitute;

namespace FriendCore.UnitTests.Messaging;

/// <summary>
///     Hiding is the private half of message removal, and the thing worth pinning down is which
///     consent refusal it implies — that judgement decides whether the image can ever come back,
///     and it is the part that is easy to get quietly wrong.
/// </summary>
public class HideMessageCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 9, 30, 0, TimeSpan.Zero);

    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IMessagingRepository _messagingRepository = Substitute.For<IMessagingRepository>();
    private readonly IBlockRepository _blockRepository = Substitute.For<IBlockRepository>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly IRealtimeNotifier _realtimeNotifier = Substitute.For<IRealtimeNotifier>();

    private readonly Ulid _hiderId = Ulid.NewUlid();
    private readonly Ulid _senderId = Ulid.NewUlid();

    private readonly Conversation _conversation;
    private readonly Message _message;

    public HideMessageCommandHandlerTests()
    {
        _currentUserService.UserId.Returns(_hiderId);
        _dateTimeProvider.UtcNow.Returns(Now);

        // The sender's message, from the hider's side of the conversation.
        _conversation = Conversation.CreateDirect(_hiderId, _senderId, Now).Value;
        _message = Message.Create(_conversation.Id, _senderId, "look at this", Now).Value;

        _messagingRepository.GetByIdAsync(_conversation.Id, Arg.Any<CancellationToken>())
            .Returns(_conversation);
        _messagingRepository.GetMessageAsync(_conversation.Id, _message.Id, Arg.Any<CancellationToken>())
            .Returns(_message);
    }

    private HideMessageCommandHandler Handler() => new(
        _currentUserService, _messagingRepository, _blockRepository, _dateTimeProvider);

    private Task<ErrorOr<Success>> HideAsync(Ulid? conversationId = null, Ulid? messageId = null) =>
        Handler().Handle(
            new HideMessageCommand(conversationId ?? _conversation.Id, messageId ?? _message.Id),
            CancellationToken.None);

    [Fact]
    public async Task Hiding_someone_elses_message_records_a_recipient_refusal()
    {
        // The whole point: after this, the sender cannot send that image again to this person.
        var result = await HideAsync();

        Assert.False(result.IsError);
        await _messagingRepository.Received(1).HideMessageAsync(
            _message.Id, _hiderId, Now, MessageDeletionSource.Recipient, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Hiding_your_own_message_records_no_refusal()
    {
        // Withdrawing your own message is not anyone declining to receive something — the same
        // judgement the sender's delete already makes, and asserted here because hiding is a new
        // call site where getting it wrong is invisible until a user is wrongly blocked later.
        _currentUserService.UserId.Returns(_senderId);

        var result = await HideAsync();

        Assert.False(result.IsError);
        await _messagingRepository.Received(1).HideMessageAsync(
            _message.Id, _senderId, Now, MessageDeletionSource.Sender, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Hiding_tells_neither_side()
    {
        await HideAsync();

        // Privacy, not laziness: a push to the sender would reveal that they acted on the
        // message. A push to the hider's other devices is unnecessary because MessageReceived
        // already never reaches your own actions.
        await _realtimeNotifier.DidNotReceiveWithAnyArgs().MessageDeletedAsync(
            default!, default!, default!, default!);
        await _realtimeNotifier.DidNotReceiveWithAnyArgs().MessageEditedAsync(
            default!, default!, default!, default!);
    }

    [Fact]
    public async Task Hiding_does_not_delete_or_edit_the_message()
    {
        await HideAsync();

        // The whole distinction from delete. If this ever starts calling SoftDeleteAsync the
        // sender loses their copy and the sender's report subject with it.
        await _messagingRepository.DidNotReceiveWithAnyArgs().SoftDeleteAsync(
            default!, default!, default!, default!);
        await _messagingRepository.DidNotReceiveWithAnyArgs().TryApplyEditAsync(
            default!, default!, default!, default!, default!, default!);
    }

    [Fact]
    public async Task Hiding_is_scoped_to_a_participant()
    {
        var outsider = Ulid.NewUlid();
        _currentUserService.UserId.Returns(outsider);

        var result = await HideAsync();

        Assert.True(result.IsError);
        Assert.Equal("Messaging.ConversationNotFound", result.FirstError.Code);
        await _messagingRepository.DidNotReceiveWithAnyArgs().HideMessageAsync(
            default!, default!, default!, default!, default!);
    }

    [Fact]
    public async Task Hiding_a_message_from_another_conversation_is_not_found()
    {
        var elsewhere = Conversation.CreateDirect(_hiderId, Ulid.NewUlid(), Now).Value;
        var theirMessage = Message.Create(elsewhere.Id, _senderId, "elsewhere", Now).Value;

        var result = await HideAsync(messageId: theirMessage.Id);

        Assert.True(result.IsError);
        Assert.Equal("Messaging.MessageNotFound", result.FirstError.Code);
    }

    [Fact]
    public async Task Hiding_is_refused_across_a_block()
    {
        // §8.2: a block makes the conversation "not exist" for both sides, writes included —
        // otherwise hiding would be a way to keep acting on a blocked conversation.
        _blockRepository.ExistsBetweenAsync(Arg.Any<Ulid>(), Arg.Any<Ulid>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await HideAsync();

        Assert.True(result.IsError);
        Assert.Equal("Messaging.ConversationNotFound", result.FirstError.Code);
        await _messagingRepository.DidNotReceiveWithAnyArgs().HideMessageAsync(
            default!, default!, default!, default!, default!);
    }

    [Fact]
    public async Task Hiding_requires_an_authenticated_caller()
    {
        _currentUserService.UserId.Returns((Ulid?)null);

        var result = await HideAsync();

        Assert.True(result.IsError);
        Assert.Equal("Common.Unauthorized", result.FirstError.Code);
    }

    [Fact]
    public async Task Hiding_a_missing_message_is_not_found()
    {
        _messagingRepository.GetMessageAsync(_conversation.Id, Arg.Any<Ulid>(), Arg.Any<CancellationToken>())
            .Returns((Message?)null);

        var result = await HideAsync();

        Assert.True(result.IsError);
        Assert.Equal("Messaging.MessageNotFound", result.FirstError.Code);
    }
}