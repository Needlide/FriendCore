using FriendCore.Application.Common;
using FriendCore.Application.Common.Options;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Application.Messaging.Commands.SendMessage;
using FriendCore.Application.Messaging.Commands.StartConversation;
using FriendCore.Application.Notifications.Events;
using FriendCore.Domain.Media;
using FriendCore.Domain.Messaging;
using FriendCore.Domain.Users;
using FriendCore.UnitTests.Users;
using MediatR;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace FriendCore.UnitTests.Messaging;

public class MessagingHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 13, 9, 0, 0, TimeSpan.Zero);

    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IBlockRepository _blockRepository = Substitute.For<IBlockRepository>();
    private readonly IMessagingRepository _messagingRepository = Substitute.For<IMessagingRepository>();
    private readonly IMediaRepository _mediaRepository = Substitute.For<IMediaRepository>();
    private readonly MediaResponseFactory _mediaResponseFactory;
    private readonly IRealtimeNotifier _realtimeNotifier = Substitute.For<IRealtimeNotifier>();
    private readonly IPublisher _publisher = Substitute.For<IPublisher>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly Ulid _userId = Ulid.NewUlid();
    private readonly User _recipient = UserTestFactory.Create(username: "bob", email: "bob@example.com");

    public MessagingHandlerTests()
    {
        _currentUserService.UserId.Returns(_userId);
        _dateTimeProvider.UtcNow.Returns(Now);
        _userRepository.GetByUsernameSlugAsync("bob", Arg.Any<CancellationToken>()).Returns(_recipient);
        // Real factory over the media substitute: variant/URL composition is the part worth
        // executing, the lookup is what we are stubbing.
        _mediaResponseFactory = new MediaResponseFactory(
            _mediaRepository, Options.Create(new StorageOptions()));
        // Default: nothing is already attached, so attachment tests opt in explicitly.
        _mediaRepository.GetAttachedIdsAsync(Arg.Any<IReadOnlyCollection<Ulid>>(), Arg.Any<CancellationToken>())
            .Returns([]);
    }

    private StartConversationCommandHandler StartHandler() =>
        new(_currentUserService, _userRepository, _blockRepository, _messagingRepository, _dateTimeProvider);

    private SendMessageCommandHandler SendHandler() =>
        new(_currentUserService, _messagingRepository, _blockRepository, _mediaRepository,
            _mediaResponseFactory, _realtimeNotifier, _publisher, _dateTimeProvider);

    [Fact]
    public async Task Start_ReturnsUnauthorized_WhenNoCurrentUser()
    {
        _currentUserService.UserId.Returns((Ulid?)null);

        var result = await StartHandler().Handle(new StartConversationCommand("bob"), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Common.Unauthorized", result.FirstError.Code);
    }

    [Fact]
    public async Task Start_ReturnsRecipientNotFound_WhenUserMissing()
    {
        _userRepository.GetByUsernameSlugAsync("ghost", Arg.Any<CancellationToken>()).Returns((User?)null);

        var result = await StartHandler().Handle(new StartConversationCommand("ghost"), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Messaging.RecipientNotFound", result.FirstError.Code);
    }

    [Fact]
    public async Task Start_ReturnsCannotMessageSelf_WhenTargetIsCurrentUser()
    {
        _currentUserService.UserId.Returns(_recipient.Id);

        var result = await StartHandler().Handle(new StartConversationCommand("bob"), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Messaging.CannotMessageSelf", result.FirstError.Code);
    }

    [Fact]
    public async Task Start_ReturnsRecipientNotFound_WhenBlocked()
    {
        // 404, not 403 — the block must not be revealed (§8.2).
        _blockRepository.ExistsBetweenAsync(_userId, _recipient.Id, Arg.Any<CancellationToken>()).Returns(true);

        var result = await StartHandler().Handle(new StartConversationCommand("bob"), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Messaging.RecipientNotFound", result.FirstError.Code);
        await _messagingRepository.DidNotReceiveWithAnyArgs().AddConversationAsync(default!, default);
    }

    [Fact]
    public async Task Start_CreatesConversation_WhenNoneExists()
    {
        _messagingRepository.FindDirectAsync(_userId, _recipient.Id, Arg.Any<CancellationToken>())
            .Returns((Conversation?)null);

        var result = await StartHandler().Handle(new StartConversationCommand("bob"), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(_recipient.Id.ToString(), result.Value.OtherParticipant.UserId);
        await _messagingRepository.Received(1).AddConversationAsync(
            Arg.Is<Conversation>(c => c.HasParticipant(_userId) && c.HasParticipant(_recipient.Id)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Start_ReturnsExisting_WithoutCreating()
    {
        var existing = Conversation.CreateDirect(_userId, _recipient.Id, Now).Value;
        _messagingRepository.FindDirectAsync(_userId, _recipient.Id, Arg.Any<CancellationToken>())
            .Returns(existing);

        var result = await StartHandler().Handle(new StartConversationCommand("bob"), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(existing.Id.ToString(), result.Value.Id);
        await _messagingRepository.DidNotReceiveWithAnyArgs().AddConversationAsync(default!, default);
    }

    [Fact]
    public async Task Send_ReturnsConversationNotFound_WhenNotParticipant()
    {
        var conversation = Conversation.CreateDirect(Ulid.NewUlid(), Ulid.NewUlid(), Now).Value;
        _messagingRepository.GetByIdAsync(conversation.Id, Arg.Any<CancellationToken>()).Returns(conversation);

        var result = await SendHandler().Handle(
            new SendMessageCommand(conversation.Id, "hi"), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Messaging.ConversationNotFound", result.FirstError.Code);
        await _messagingRepository.DidNotReceiveWithAnyArgs().AddMessageAsync(default!, default);
    }

    [Fact]
    public async Task Send_ReturnsConversationNotFound_WhenBlockedAfterCreation()
    {
        var conversation = Conversation.CreateDirect(_userId, _recipient.Id, Now).Value;
        _messagingRepository.GetByIdAsync(conversation.Id, Arg.Any<CancellationToken>()).Returns(conversation);
        _blockRepository.ExistsBetweenAsync(_userId, _recipient.Id, Arg.Any<CancellationToken>()).Returns(true);

        var result = await SendHandler().Handle(
            new SendMessageCommand(conversation.Id, "hi"), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Messaging.ConversationNotFound", result.FirstError.Code);
        await _messagingRepository.DidNotReceiveWithAnyArgs().AddMessageAsync(default!, default);
    }

    [Fact]
    public async Task Send_PersistsMessage_AndBumpsConversation()
    {
        var conversation = Conversation.CreateDirect(_userId, _recipient.Id, Now).Value;
        _messagingRepository.GetByIdAsync(conversation.Id, Arg.Any<CancellationToken>()).Returns(conversation);

        var result = await SendHandler().Handle(
            new SendMessageCommand(conversation.Id, "  hello  "), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal("hello", result.Value.Content);
        Assert.Equal(_userId.ToString(), result.Value.SenderId);
        Assert.Equal(Now, conversation.LastMessageAt);
        await _messagingRepository.Received(1).AddMessageAsync(
            Arg.Is<Message>(m => m.ConversationId == conversation.Id && m.SenderId == _userId),
            Arg.Any<CancellationToken>());
        await _publisher.Received(1).Publish(
            Arg.Is<MessageSentEvent>(e => e.RecipientId == _recipient.Id && e.SenderId == _userId),
            Arg.Any<CancellationToken>());
        await _realtimeNotifier.Received(1).MessageReceivedAsync(
            _recipient.Id, Arg.Is<MessageResponse>(m => m.Content == "hello"), Arg.Any<CancellationToken>());
    }

    // ── Attachments ────────────────────────────────────────────────────────────
    // DMs reuse MediaItem wholesale, so the same four gates posts apply have to hold
    // here or an upload could be attached by someone who does not own it.

    private static MediaItem Ready(MediaItem media)
    {
        media.MarkReady([]);
        return media;
    }

    private MediaItem ReadyMedia(Ulid? uploader = null) =>
        Ready(MediaItem.Create(uploader ?? _userId, "x.png", "image/png", "png", false, false, Now));

    private Conversation GivenConversation()
    {
        var conversation = Conversation.CreateDirect(_userId, _recipient.Id, Now).Value;
        _messagingRepository.GetByIdAsync(conversation.Id, Arg.Any<CancellationToken>()).Returns(conversation);
        return conversation;
    }

    private void MediaReturns(MediaItem media) =>
        _mediaRepository.GetByIdsAsync(Arg.Any<IReadOnlyCollection<Ulid>>(), Arg.Any<CancellationToken>())
            .Returns([media]);

    [Fact]
    public async Task Send_WithAttachmentAndNoText_Succeeds()
    {
        var conversation = GivenConversation();
        var media = ReadyMedia();
        MediaReturns(media);

        var result = await SendHandler().Handle(
            new SendMessageCommand(conversation.Id, null, media.Id), CancellationToken.None);

        Assert.False(result.IsError);
        // Content stays null: no "Sent a file" placeholder standing in for real text.
        Assert.Null(result.Value.Content);
        Assert.NotNull(result.Value.Media);
        Assert.Equal(media.Id.ToString(), result.Value.Media!.Id);
    }

    [Fact]
    public async Task Send_WithAttachment_KeepsTextToo()
    {
        var conversation = GivenConversation();
        var media = ReadyMedia();
        MediaReturns(media);

        var result = await SendHandler().Handle(
            new SendMessageCommand(conversation.Id, "look", media.Id), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal("look", result.Value.Content);
        Assert.NotNull(result.Value.Media);
    }

    [Fact]
    public async Task Send_WithUnknownAttachment_ReturnsNotFound()
    {
        var conversation = GivenConversation();
        var mediaId = Ulid.NewUlid();
        _mediaRepository.GetByIdsAsync(Arg.Any<IReadOnlyCollection<Ulid>>(), Arg.Any<CancellationToken>())
            .Returns([]);

        var result = await SendHandler().Handle(
            new SendMessageCommand(conversation.Id, "hi", mediaId), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Media.NotFound", result.FirstError.Code);
        await _messagingRepository.DidNotReceiveWithAnyArgs().AddMessageAsync(default!, default);
    }

    [Fact]
    public async Task Send_WithSomeoneElsesAttachment_IsRejected()
    {
        var conversation = GivenConversation();
        var media = ReadyMedia(Ulid.NewUlid());
        MediaReturns(media);

        var result = await SendHandler().Handle(
            new SendMessageCommand(conversation.Id, "hi", media.Id), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Media.NotOwner", result.FirstError.Code);
        await _messagingRepository.DidNotReceiveWithAnyArgs().AddMessageAsync(default!, default);
    }

    [Fact]
    public async Task Send_WithStillProcessingAttachment_IsRejected()
    {
        var conversation = GivenConversation();
        // Deliberately not MarkReady — still Processing.
        var media = MediaItem.Create(_userId, "x.png", "image/png", "png", false, false, Now);
        MediaReturns(media);

        var result = await SendHandler().Handle(
            new SendMessageCommand(conversation.Id, "hi", media.Id), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Media.NotReady", result.FirstError.Code);
    }

    [Fact]
    public async Task Send_WithAlreadyAttachedAttachment_ReturnsConflict()
    {
        var conversation = GivenConversation();
        var media = ReadyMedia();
        MediaReturns(media);
        _mediaRepository.GetAttachedIdsAsync(Arg.Any<IReadOnlyCollection<Ulid>>(), Arg.Any<CancellationToken>())
            .Returns([media.Id]);

        var result = await SendHandler().Handle(
            new SendMessageCommand(conversation.Id, "hi", media.Id), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Media.AlreadyAttached", result.FirstError.Code);
        await _messagingRepository.DidNotReceiveWithAnyArgs().AddMessageAsync(default!, default);
    }

    [Fact]
    public async Task Send_WithNeitherTextNorAttachment_ReturnsError()
    {
        var conversation = GivenConversation();

        var result = await SendHandler().Handle(
            new SendMessageCommand(conversation.Id, "   ", null), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Messaging.MessageEmpty", result.FirstError.Code);
    }
}
