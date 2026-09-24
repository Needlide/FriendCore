using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Users.Commands.ClearNotificationSnooze;
using FriendCore.Application.Users.Commands.SetNotificationSnooze;
using FriendCore.Domain.Users;
using NSubstitute;

namespace FriendCore.UnitTests.Users;

public class NotificationSnoozeCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 2, 9, 0, 0, TimeSpan.Zero);

    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly Ulid _userId = Ulid.NewUlid();
    private readonly UserPreferences _preferences;

    public NotificationSnoozeCommandHandlerTests()
    {
        _preferences = UserPreferences.CreateDefault(_userId, Now.AddDays(-1));
        _currentUserService.UserId.Returns(_userId);
        _dateTimeProvider.UtcNow.Returns(Now);
        _userRepository.GetPreferencesAsync(_userId, Arg.Any<CancellationToken>()).Returns(_preferences);
    }

    [Fact]
    public async Task Set_ReturnsUnauthorized_WhenNoCurrentUser()
    {
        _currentUserService.UserId.Returns((Ulid?)null);

        var result = await new SetNotificationSnoozeCommandHandler(
                _currentUserService, _userRepository, _dateTimeProvider)
            .Handle(new SetNotificationSnoozeCommand(Now.AddHours(2)), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Common.Unauthorized", result.FirstError.Code);
    }

    [Fact]
    public async Task Set_RejectsPast_AndBeyond24h()
    {
        var handler = new SetNotificationSnoozeCommandHandler(
            _currentUserService, _userRepository, _dateTimeProvider);

        var past = await handler.Handle(
            new SetNotificationSnoozeCommand(Now.AddMinutes(-1)), CancellationToken.None);
        Assert.Equal("User.InvalidSnoozeWindow", past.FirstError.Code);

        var far = await handler.Handle(
            new SetNotificationSnoozeCommand(Now.AddHours(25)), CancellationToken.None);
        Assert.Equal("User.InvalidSnoozeWindow", far.FirstError.Code);

        Assert.Null(_preferences.SnoozedUntilUtc);
        await _userRepository.DidNotReceiveWithAnyArgs().UpdatePreferencesAsync(default);
    }

    [Fact]
    public async Task Set_StoresUntil_AndReturnsIt()
    {
        var until = Now.AddHours(4);
        var result = await new SetNotificationSnoozeCommandHandler(
                _currentUserService, _userRepository, _dateTimeProvider)
            .Handle(new SetNotificationSnoozeCommand(until), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(until, result.Value.SnoozedUntil);
        Assert.Equal(until, _preferences.SnoozedUntilUtc);
        await _userRepository.Received(1).UpdatePreferencesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Clear_NullsUntil()
    {
        _preferences.SetSnoozedUntil(Now.AddHours(2), Now);

        var result = await new ClearNotificationSnoozeCommandHandler(
                _currentUserService, _userRepository, _dateTimeProvider)
            .Handle(new ClearNotificationSnoozeCommand(), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Null(result.Value.SnoozedUntil);
        Assert.Null(_preferences.SnoozedUntilUtc);
    }
}
