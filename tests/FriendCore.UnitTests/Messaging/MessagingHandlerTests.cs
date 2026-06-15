using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Messaging.Commands.SendMessage;
using FriendCore.Application.Messaging.Commands.StartConversation;
using FriendCore.Domain.Messaging;
using FriendCore.Domain.Users;
using FriendCore.UnitTests.Users;
using NSubstitute;

namespace FriendCore.UnitTests.Messaging;

public class MessagingHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 13, 9, 0, 0, TimeSpan.Zero);

    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IBlockRepository _blockRepository = Substitute.For<IBlockRepository>();
    private readonly IMessagingRepository _messagingRepository = Substitute.For<IMessagingRepository>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly Ulid _userId = Ulid.NewUlid();
    private readonly User _recipient = UserTestFactory.Create(username: "bob", email: "bob@example.com");

    public MessagingHandlerTests()
    {
        _currentUserService.UserId.Returns(_userId);
        _dateTimeProvider.UtcNow.Returns(Now);
        _userRepository.GetByUsernameSlugAsync("bob", Arg.Any<CancellationToken>()).Returns(_recipient);
    }

    private StartConversationCommandHandler StartHandler() =>
        new(_currentUserService, _userRepository, _blockRepository, _messagingRepository, _dateTimeProvider);

    private SendMessageCommandHandler SendHandler() =>
        new(_currentUserService, _messagingRepository, _blockRepository, _dateTimeProvider);

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
    }
}
