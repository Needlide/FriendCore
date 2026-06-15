using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Moderation.Commands.ChangeUserRole;
using FriendCore.Application.Moderation.Common;
using FriendCore.Domain.Moderation;
using FriendCore.Domain.Users;
using FriendCore.UnitTests.Users;
using NSubstitute;

namespace FriendCore.UnitTests.Moderation;

public class ChangeUserRoleHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 16, 9, 0, 0, TimeSpan.Zero);

    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IAdminAuditLogRepository _auditLogRepository = Substitute.For<IAdminAuditLogRepository>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly Ulid _adminId = Ulid.NewUlid();

    public ChangeUserRoleHandlerTests()
    {
        _currentUserService.UserId.Returns(_adminId);
        _dateTimeProvider.UtcNow.Returns(Now);
    }

    private ChangeUserRoleCommandHandler CreateHandler() =>
        new(_currentUserService, _userRepository, _auditLogRepository, _dateTimeProvider);

    [Fact]
    public async Task Handle_ReturnsNotFound_WhenTargetMissing()
    {
        _userRepository.GetByUsernameSlugIgnoringBlocksAsync("ghost", Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var result = await CreateHandler().Handle(
            new ChangeUserRoleCommand("ghost", UserRoleInput.Moderator, "promote"), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("User.NotFound", result.FirstError.Code);
    }

    [Fact]
    public async Task Handle_ReturnsCannotChangeOwnRole()
    {
        var self = UserTestFactory.Create(username: "boss");
        _currentUserService.UserId.Returns(self.Id);
        _userRepository.GetByUsernameSlugIgnoringBlocksAsync("boss", Arg.Any<CancellationToken>()).Returns(self);

        var result = await CreateHandler().Handle(
            new ChangeUserRoleCommand("boss", UserRoleInput.User, "demote self"), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Moderation.CannotChangeOwnRole", result.FirstError.Code);
        await _auditLogRepository.DidNotReceiveWithAnyArgs().AddAndSaveAsync(default!, default);
    }

    [Fact]
    public async Task Handle_PromotesTargetAndLogsTheChange()
    {
        var target = UserTestFactory.Create(username: "bob");
        _userRepository.GetByUsernameSlugIgnoringBlocksAsync("bob", Arg.Any<CancellationToken>()).Returns(target);

        var result = await CreateHandler().Handle(
            new ChangeUserRoleCommand("bob", UserRoleInput.Moderator, "trusted contributor"), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(UserRole.Moderator, target.Role);
        await _auditLogRepository.Received(1).AddAndSaveAsync(
            Arg.Is<AdminAuditLog>(e =>
                e.Action == ModerationActionType.RoleChanged
                && e.TargetType == ReportTargetType.User
                && e.TargetId == target.Id
                && e.Reason == "trusted contributor"
                && e.ModeratorId == _adminId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_IsIdempotent_WhenRoleUnchanged()
    {
        var target = UserTestFactory.Create(username: "bob");
        target.AssignRole(UserRole.Moderator);
        _userRepository.GetByUsernameSlugIgnoringBlocksAsync("bob", Arg.Any<CancellationToken>()).Returns(target);

        var result = await CreateHandler().Handle(
            new ChangeUserRoleCommand("bob", UserRoleInput.Moderator, "no change"), CancellationToken.None);

        Assert.False(result.IsError);
        await _auditLogRepository.DidNotReceiveWithAnyArgs().AddAndSaveAsync(default!, default);
    }
}
