using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Users.Queries.GetPreferences;
using FriendCore.Domain.Users;
using NSubstitute;

namespace FriendCore.UnitTests.Users;

public class GetPreferencesQueryHandlerTests
{
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly User _user = UserTestFactory.Create();
    private readonly UserPreferences _preferences;

    public GetPreferencesQueryHandlerTests()
    {
        _preferences = UserPreferences.CreateDefault(_user.Id, UserTestFactory.DefaultCreatedAt);
        _currentUserService.UserId.Returns(_user.Id);
        _userRepository.GetByIdAsync(_user.Id, Arg.Any<CancellationToken>()).Returns(_user);
        _userRepository.GetPreferencesAsync(_user.Id, Arg.Any<CancellationToken>()).Returns(_preferences);
    }

    [Fact]
    public async Task Handle_ReturnsUnauthorized_WhenNoCurrentUser()
    {
        _currentUserService.UserId.Returns((Ulid?)null);

        var result = await new GetPreferencesQueryHandler(_currentUserService, _userRepository)
            .Handle(new GetPreferencesQuery(), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Common.Unauthorized", result.FirstError.Code);
    }

    [Fact]
    public async Task Handle_ReturnsPreferences_WithContentPreference()
    {
        var result = await new GetPreferencesQueryHandler(_currentUserService, _userRepository)
            .Handle(new GetPreferencesQuery(), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(_preferences.HideFlashingMedia, result.Value.HideFlashingMedia);
        Assert.Equal(_preferences.HideIntenseMotionMedia, result.Value.HideIntenseMotionMedia);
        Assert.True(result.Value.HideIntenseMotionMedia);
        Assert.Equal("General", result.Value.ContentPreference);
    }
}
