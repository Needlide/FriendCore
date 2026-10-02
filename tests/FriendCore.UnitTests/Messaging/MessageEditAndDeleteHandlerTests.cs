using ErrorOr;
using FriendCore.Application.Common;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Application.Common.Options;
using FriendCore.Application.Messaging.Commands.DeleteMessage;
using FriendCore.Application.Messaging.Commands.EditMessage;
using FriendCore.Domain.Media;
using FriendCore.Domain.Messaging;
using FriendCore.Domain.Users;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace FriendCore.UnitTests.Messaging;

/// <summary>
/// Edit and delete share the same guard order, so they are tested side by side:
/// unauthenticated → conversation missing / not a participant → blocked → message missing →
/// not the sender → domain refusal.
/// </summary>
public class MessageEditAndDeleteHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = Now.AddMinutes(5);

    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IBlockRepository _blockRepository = Substitute.For<IBlockRepository>();
    private readonly IMessagingRepository _messagingRepository = Substitute.For<IMessagingRepository>();
    private readonly IMediaRepository _mediaRepository = Substitute.For<IMediaRepository>();
    private readonly IRealtimeNotifier _realtimeNotifier = Substitute.For<IRealtimeNotifier>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly MediaResponseFactory _mediaResponseFactory;

    private readonly Ulid _senderId = Ulid.NewUlid();
    private readonly Ulid _otherId = Ulid.NewUlid();

    // Both ids are read back off the aggregates rather than invented: CreateDirect and
    // Message.Create each mint their own id, and the handler looks entities up (and pushes
    // them) by those — a made-up key would simply never match.
    private readonly Ulid _conversationId;
    private readonly Ulid _messageId;
    private readonly Message _message;
    private readonly Conversation _conversation;

    public MessageEditAndDeleteHandlerTests()
    {
        _currentUserService.UserId.Returns(_senderId);
        _dateTimeProvider.UtcNow.Returns(Now);
        _mediaResponseFactory = new MediaResponseFactory(
            _mediaRepository, Options.Create(new StorageOptions()));

        _conversation = Conversation.CreateDirect(_senderId, _otherId, Now).Value;
        _conversationId = _conversation.Id;
        _message = Message.Create(_conversationId, _senderId, "original text", Now).Value;
        _messageId = _message.Id;

        _messagingRepository.GetByIdAsync(_conversationId, Arg.Any<CancellationToken>())
            .Returns(_conversation);
        _messagingRepository.GetMessageAsync(_conversationId, _messageId, Arg.Any<CancellationToken>())
            .Returns(_message);
    }

    private DeleteMessageCommandHandler DeleteHandler() =>
        new(_currentUserService, _messagingRepository, _blockRepository, _realtimeNotifier, _dateTimeProvider);

    private EditMessageCommandHandler EditHandler() =>
        new(_currentUserService, _messagingRepository, _blockRepository, _mediaResponseFactory,
            _realtimeNotifier, _dateTimeProvider);

    private Task<ErrorOr<MessageResponse>> Edit(string content) =>
        EditHandler().Handle(
            new EditMessageCommand(_conversationId, _messageId, content), CancellationToken.None);

    [Fact]
    public async Task Delete_ReturnsUnauthorized_WhenNoCurrentUser()
    {
        _currentUserService.UserId.Returns((Ulid?)null);

        var result = await DeleteHandler().Handle(
            new DeleteMessageCommand(_conversationId, _messageId), CancellationToken.None);

        Assert.Equal("Common.Unauthorized", result.FirstError.Code);
    }

    [Fact]
    public async Task Delete_ReturnsConversationNotFound_WhenUserIsNotAParticipant()
    {
        // An outsider passing someone else's conversation id learns nothing.
        _currentUserService.UserId.Returns(Ulid.NewUlid());

        var result = await DeleteHandler().Handle(
            new DeleteMessageCommand(_conversationId, _messageId), CancellationToken.None);

        Assert.Equal("Messaging.ConversationNotFound", result.FirstError.Code);
    }

    [Fact]
    public async Task Delete_ReturnsConversationNotFound_WhenBlocked()
    {
        // §8.2: a block makes the conversation stop existing for both sides, writes included.
        _blockRepository.ExistsBetweenAsync(_senderId, _otherId, Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await DeleteHandler().Handle(
            new DeleteMessageCommand(_conversationId, _messageId), CancellationToken.None);

        Assert.Equal("Messaging.ConversationNotFound", result.FirstError.Code);
    }

    [Fact]
    public async Task Delete_ReturnsMessageNotFound_WhenTheMessageIsGone()
    {
        _messagingRepository.GetMessageAsync(_conversationId, _messageId, Arg.Any<CancellationToken>())
            .Returns((Message?)null);

        var result = await DeleteHandler().Handle(
            new DeleteMessageCommand(_conversationId, _messageId), CancellationToken.None);

        Assert.Equal("Messaging.MessageNotFound", result.FirstError.Code);
    }

    [Fact]
    public async Task Delete_ReturnsForbidden_WhenTheMessageIsNotMine()
    {
        var theirs = Message.Create(_conversationId, _otherId, "theirs", Now).Value;
        _messagingRepository.GetMessageAsync(_conversationId, _messageId, Arg.Any<CancellationToken>())
            .Returns(theirs);

        var result = await DeleteHandler().Handle(
            new DeleteMessageCommand(_conversationId, _messageId), CancellationToken.None);

        Assert.Equal("Messaging.NotMessageSender", result.FirstError.Code);
    }

    [Fact]
    public async Task Delete_RoutesThroughTheRepositoryWithTheInjectedTimestamp()
    {
        await DeleteHandler().Handle(
            new DeleteMessageCommand(_conversationId, _messageId), CancellationToken.None);

        // Scrubbing the text is the aggregate's job and is covered as a domain test; what the
        // handler owes is that it goes through the soft-delete path (never a hard delete) and
        // uses the injected clock rather than its own.
        await _messagingRepository.Received(1).SoftDeleteAsync(
            _message, Now, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_PushesToBothParticipants_SoTheRecipientsThreadAlsoDropsIt()
    {
        await DeleteHandler().Handle(
            new DeleteMessageCommand(_conversationId, _messageId), CancellationToken.None);

        // MessageReceived reaches only the recipient; a delete has to reach the other side too.
        await _realtimeNotifier.Received(1).MessageDeletedAsync(
            _conversationId,
            Arg.Is<IEnumerable<Ulid>>(ids => ids.Contains(_senderId) && ids.Contains(_otherId)),
            _messageId.ToString(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Delete_IsIdempotent_ForAnAlreadyDeletedMessage()
    {
        _message.MarkDeleted(Now);

        _message.MarkDeleted(Later);

        // A double submit must not move the deletion time or resurrect the text.
        Assert.Equal(Now, _message.DeletedAt);
        Assert.Null(_message.Content);
    }

    [Fact]
    public async Task Edit_ReplacesTheTextAndStampsEditedAt()
    {
        var result = await EditHandler().Handle(
            new EditMessageCommand(_conversationId, _messageId, "  corrected text  "), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal("corrected text", _message.Content);
        Assert.Equal(Now, _message.EditedAt);
        Assert.Equal("corrected text", result.Value.Content);
        Assert.Equal(Now, result.Value.EditedAt);
    }

    [Fact]
    public async Task Edit_KeepsTheFirstEditTime_SoTheMarkerDoesNotDrift()
    {
        await Edit("first correction");
        _dateTimeProvider.UtcNow.Returns(Later);

        await Edit("second correction");

        Assert.Equal("second correction", _message.Content);
        Assert.Equal(Now, _message.EditedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Edit_RejectsEmptyText(string content)
    {
        var result = await Edit(content);

        Assert.Equal("Messaging.MessageEmpty", result.FirstError.Code);
        Assert.Equal("original text", _message.Content);
    }

    [Fact]
    public async Task Edit_RejectsTextBeyondTheCap()
    {
        var result = await Edit(new string('x', Message.MaxContentLength + 1));

        Assert.Equal("Messaging.MessageTooLong", result.FirstError.Code);
    }

    [Fact]
    public async Task Edit_RejectsAnAttachmentOnlyMessage()
    {
        var mediaId = Ulid.NewUlid();
        var image = Message.Create(_conversationId, _senderId, null, Now, mediaId).Value;
        _messagingRepository.GetMessageAsync(_conversationId, _messageId, Arg.Any<CancellationToken>())
            .Returns(image);

        var result = await Edit("a caption I never typed");

        // Adding a caption after the fact would put words in the sender's mouth on a bubble
        // they sent bare, so the whole edit is refused rather than half-applied.
        Assert.Equal("Messaging.MessageNotEditable", result.FirstError.Code);
        Assert.Null(image.Content);
    }

    [Fact]
    public async Task Edit_AllowsRetitlingAMessageThatHadBothTextAndAnImage()
    {
        var mediaId = Ulid.NewUlid();
        var captioned = Message.Create(_conversationId, _senderId, "look", Now, mediaId).Value;
        _messagingRepository.GetMessageAsync(_conversationId, _messageId, Arg.Any<CancellationToken>())
            .Returns(captioned);

        var result = await EditHandler().Handle(
            new EditMessageCommand(_conversationId, _messageId, "look at this"), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal("look at this", captioned.Content);
    }

    [Fact]
    public async Task Edit_ReturnsMessageNotFound_ForADeletedMessage()
    {
        _message.MarkDeleted(Now);

        var result = await Edit("resurrect me");

        Assert.Equal("Messaging.MessageNotFound", result.FirstError.Code);
    }

    [Fact]
    public async Task Edit_ReturnsForbidden_WhenTheMessageIsNotMine()
    {
        var theirs = Message.Create(_conversationId, _otherId, "theirs", Now).Value;
        _messagingRepository.GetMessageAsync(_conversationId, _messageId, Arg.Any<CancellationToken>())
            .Returns(theirs);

        var result = await Edit("rewriting your words");

        Assert.Equal("Messaging.NotMessageSender", result.FirstError.Code);
    }

    [Fact]
    public async Task Edit_PushesToBothParticipants_SoTheSendersOtherDevicesCatchUp()
    {
        await Edit("corrected text");

        await _realtimeNotifier.Received(1).MessageEditedAsync(
            _conversationId,
            Arg.Is<IEnumerable<Ulid>>(ids => ids.Contains(_senderId) && ids.Contains(_otherId)),
            Arg.Is<MessageResponse>(response => response.Content == "corrected text"),
            Arg.Any<CancellationToken>());
    }
}