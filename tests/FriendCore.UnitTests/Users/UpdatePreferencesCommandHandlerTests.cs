using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Users.Commands.UpdatePreferences;
using FriendCore.Domain.Users;
using NSubstitute;

namespace FriendCore.UnitTests.Users;

public class UpdatePreferencesCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 12, 9, 0, 0, TimeSpan.Zero);

    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly User _user = UserTestFactory.Create();
    private readonly UserPreferences _preferences;

    public UpdatePreferencesCommandHandlerTests()
    {
        _preferences = UserPreferences.CreateDefault(_user.Id, Now.AddDays(-1));
        _currentUserService.UserId.Returns(_user.Id);
        _dateTimeProvider.UtcNow.Returns(Now);
        _userRepository.GetByIdAsync(_user.Id, Arg.Any<CancellationToken>()).Returns(_user);
        _userRepository.GetPreferencesAsync(_user.Id, Arg.Any<CancellationToken>()).Returns(_preferences);
    }

    private UpdatePreferencesCommandHandler CreateHandler() =>
        new(_currentUserService, _userRepository, _dateTimeProvider);

    [Fact]
    public async Task Handle_ReturnsUnauthorized_WhenNoCurrentUser()
    {
        _currentUserService.UserId.Returns((Ulid?)null);

        var result = await CreateHandler().Handle(
            new UpdatePreferencesCommand(true, false, true, false, UserContentPreference.General),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Common.Unauthorized", result.FirstError.Code);
    }

    [Fact]
    public async Task Handle_UpdatesContentPreferenceOnUser_AndTogglesOnPreferences()
    {
        var result = await CreateHandler().Handle(
            new UpdatePreferencesCommand(
                HideFlashingMedia: false,
                ReduceMotion: true,
                DisableAutoplay: false,
                ReduceVisualNoise: true,
                UserContentPreference.Explicit),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(ContentPreference.Explicit, _user.ContentPreference);
        Assert.False(_preferences.HideFlashingMedia);
        Assert.True(_preferences.ReduceMotion);
        Assert.False(_preferences.DisableAutoplay);
        Assert.True(_preferences.ReduceVisualNoise);
        Assert.Equal("Explicit", result.Value.ContentPreference);
        await _userRepository.Received(1).UpdateAsync(_user, Arg.Any<CancellationToken>());
    }
}
