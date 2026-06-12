using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Users.Commands.UpdateKeywordFilters;
using FriendCore.Application.Users.Queries.GetKeywordFilters;
using FriendCore.Domain.Users;
using NSubstitute;

namespace FriendCore.UnitTests.Users;

public class KeywordFiltersHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 12, 9, 0, 0, TimeSpan.Zero);

    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly Ulid _userId = Ulid.NewUlid();
    private readonly UserPreferences _preferences;

    public KeywordFiltersHandlerTests()
    {
        _preferences = UserPreferences.CreateDefault(_userId, Now.AddDays(-1));
        _currentUserService.UserId.Returns(_userId);
        _dateTimeProvider.UtcNow.Returns(Now);
        _userRepository.GetPreferencesAsync(_userId, Arg.Any<CancellationToken>()).Returns(_preferences);
    }

    private UpdateKeywordFiltersCommandHandler UpdateHandler() =>
        new(_currentUserService, _userRepository, _dateTimeProvider);

    private GetKeywordFiltersQueryHandler GetHandler() =>
        new(_currentUserService, _userRepository);

    [Fact]
    public async Task Update_ReturnsUnauthorized_WhenNoCurrentUser()
    {
        _currentUserService.UserId.Returns((Ulid?)null);

        var result = await UpdateHandler().Handle(
            new UpdateKeywordFiltersCommand(["spoilers"]), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Common.Unauthorized", result.FirstError.Code);
    }

    [Fact]
    public async Task Update_PersistsNormalizedSet_AndReturnsIt()
    {
        var result = await UpdateHandler().Handle(
            new UpdateKeywordFiltersCommand(["  Spoilers ", "wasps"]), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(["spoilers", "wasps"], result.Value.Keywords);
        await _userRepository.Received(1).UpdatePreferencesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_PropagatesValidationError_WithoutPersisting()
    {
        var tooMany = Enumerable.Range(0, UserPreferences.MaxKeywordFilters + 1)
            .Select(i => $"keyword-{i}")
            .ToList();

        var result = await UpdateHandler().Handle(
            new UpdateKeywordFiltersCommand(tooMany), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("User.TooManyKeywordFilters", result.FirstError.Code);
        await _userRepository.DidNotReceive().UpdatePreferencesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Get_ReturnsCurrentFilters()
    {
        _preferences.SetKeywordFilters(["spoilers"], Now);

        var result = await GetHandler().Handle(new GetKeywordFiltersQuery(), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(["spoilers"], result.Value.Keywords);
    }

    [Fact]
    public async Task Get_ReturnsUnauthorized_WhenNoCurrentUser()
    {
        _currentUserService.UserId.Returns((Ulid?)null);

        var result = await GetHandler().Handle(new GetKeywordFiltersQuery(), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Common.Unauthorized", result.FirstError.Code);
    }
}
