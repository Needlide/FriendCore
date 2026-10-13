using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Messaging.Commands.StartSupportConversation;
using FriendCore.Domain.Messaging;
using FriendCore.Domain.Users;
using FriendCore.UnitTests.Users;
using NSubstitute;

namespace FriendCore.UnitTests.Messaging;

public class StartSupportConversationHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 13, 9, 0, 0, TimeSpan.Zero);

    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IMessagingRepository _messagingRepository = Substitute.For<IMessagingRepository>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly Ulid _userId = Ulid.NewUlid();
    private readonly User _admin = UserTestFactory.Create(username: "teamsupport", email: "support@example.com");

    public StartSupportConversationHandlerTests()
    {
        _admin.AssignRole(UserRole.Admin);
        _currentUserService.UserId.Returns(_userId);
        _dateTimeProvider.UtcNow.Returns(Now);
        _userRepository.GetFirstAdminAsync(Arg.Any<CancellationToken>()).Returns(_admin);
    }

    private StartSupportConversationCommandHandler CreateHandler() =>
        new(_currentUserService, _userRepository, _messagingRepository, _dateTimeProvider);

    [Fact]
    public async Task Handle_ReturnsUnauthorized_WhenNoCurrentUser()
    {
        _currentUserService.UserId.Returns((Ulid?)null);

        var result = await CreateHandler().Handle(new StartSupportConversationCommand(), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Common.Unauthorized", result.FirstError.Code);
    }

    [Fact]
    public async Task Handle_ReturnsSupportUnavailable_WhenNoAdminExists()
    {
        _userRepository.GetFirstAdminAsync(Arg.Any<CancellationToken>()).Returns((User?)null);

        var result = await CreateHandler().Handle(new StartSupportConversationCommand(), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Messaging.SupportUnavailable", result.FirstError.Code);
        await _messagingRepository.DidNotReceiveWithAnyArgs().AddConversationOrGetExistingAsync(default!, default);
    }

    [Fact]
    public async Task Handle_ReturnsSupportUnavailable_WhenAdminBanned()
    {
        var banned = UserTestFactory.CreateBanned();
        banned.AssignRole(UserRole.Admin);
        _userRepository.GetFirstAdminAsync(Arg.Any<CancellationToken>()).Returns(banned);

        var result = await CreateHandler().Handle(new StartSupportConversationCommand(), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Messaging.SupportUnavailable", result.FirstError.Code);
    }

    [Fact]
    public async Task Handle_ReturnsCannotMessageSelf_WhenCallerIsTheAdmin()
    {
        _currentUserService.UserId.Returns(_admin.Id);

        var result = await CreateHandler().Handle(new StartSupportConversationCommand(), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Messaging.CannotMessageSelf", result.FirstError.Code);
    }

    [Fact]
    public async Task Handle_CreatesConversation_WhenNoneExists()
    {
        _messagingRepository.FindDirectAsync(_userId, _admin.Id, Arg.Any<CancellationToken>())
            .Returns((Conversation?)null);

        // The repository hands the conversation BACK and the handler uses that returned instance —
        // the same race recovery as a normal conversation, so the stub must return one.
        _messagingRepository
            .AddConversationOrGetExistingAsync(Arg.Any<Conversation>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Conversation>());

        var result = await CreateHandler().Handle(new StartSupportConversationCommand(), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(_admin.Id.ToString(), result.Value.OtherParticipant.UserId);
        await _messagingRepository.Received(1).AddConversationOrGetExistingAsync(
            Arg.Is<Conversation>(c => c.HasParticipant(_userId) && c.HasParticipant(_admin.Id)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ReturnsExisting_WithoutCreating()
    {
        var existing = Conversation.CreateDirect(_userId, _admin.Id, Now).Value;
        _messagingRepository.FindDirectAsync(_userId, _admin.Id, Arg.Any<CancellationToken>())
            .Returns(existing);

        var result = await CreateHandler().Handle(new StartSupportConversationCommand(), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(existing.Id.ToString(), result.Value.Id);
        await _messagingRepository.DidNotReceiveWithAnyArgs().AddConversationOrGetExistingAsync(default!, default);
    }
}
