using FriendCore.Domain.Users;

namespace FriendCore.UnitTests.Users;

public class UserPreferencesKeywordFilterTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 12, 9, 0, 0, TimeSpan.Zero);

    private static UserPreferences CreatePreferences() =>
        UserPreferences.CreateDefault(Ulid.NewUlid(), Now.AddDays(-1));

    [Fact]
    public void SetKeywordFilters_NormalizesTrimsAndLowercases()
    {
        var preferences = CreatePreferences();

        var result = preferences.SetKeywordFilters(["  Spoilers  ", "WASPS"], Now);

        Assert.False(result.IsError);
        Assert.Equal(["spoilers", "wasps"], preferences.KeywordFilters);
        Assert.Equal(Now, preferences.UpdatedAt);
    }

    [Fact]
    public void SetKeywordFilters_DropsEmptiesAndDuplicates()
    {
        var preferences = CreatePreferences();

        var result = preferences.SetKeywordFilters(["spoilers", "SPOILERS", "  ", ""], Now);

        Assert.False(result.IsError);
        Assert.Equal(["spoilers"], preferences.KeywordFilters);
    }

    [Fact]
    public void SetKeywordFilters_StripsBackslashes()
    {
        // Backslash is the ILIKE escape character; a dangling one would break the SQL pattern.
        var preferences = CreatePreferences();

        var result = preferences.SetKeywordFilters([@"spoi\lers", @"trailing\"], Now);

        Assert.False(result.IsError);
        Assert.Equal(["spoilers", "trailing"], preferences.KeywordFilters);
    }

    [Fact]
    public void SetKeywordFilters_RejectsTooManyFilters()
    {
        var preferences = CreatePreferences();
        var keywords = Enumerable.Range(0, UserPreferences.MaxKeywordFilters + 1)
            .Select(i => $"keyword-{i}")
            .ToList();

        var result = preferences.SetKeywordFilters(keywords, Now);

        Assert.True(result.IsError);
        Assert.Equal("User.TooManyKeywordFilters", result.FirstError.Code);
        Assert.Empty(preferences.KeywordFilters);
    }

    [Fact]
    public void SetKeywordFilters_RejectsOverlongKeyword()
    {
        var preferences = CreatePreferences();

        var result = preferences.SetKeywordFilters(
            [new string('x', UserPreferences.MaxKeywordFilterLength + 1)], Now);

        Assert.True(result.IsError);
        Assert.Equal("User.KeywordFilterTooLong", result.FirstError.Code);
    }

    [Fact]
    public void SetKeywordFilters_AcceptsExactlyTheCaps()
    {
        var preferences = CreatePreferences();
        var keywords = Enumerable.Range(0, UserPreferences.MaxKeywordFilters)
            .Select(i => $"keyword-{i}")
            .ToList();
        keywords[0] = new string('x', UserPreferences.MaxKeywordFilterLength);

        var result = preferences.SetKeywordFilters(keywords, Now);

        Assert.False(result.IsError);
        Assert.Equal(UserPreferences.MaxKeywordFilters, preferences.KeywordFilters.Count);
    }

    [Fact]
    public void SetKeywordFilters_EmptyListClearsFilters()
    {
        var preferences = CreatePreferences();
        preferences.SetKeywordFilters(["spoilers"], Now.AddMinutes(-5));

        var result = preferences.SetKeywordFilters([], Now);

        Assert.False(result.IsError);
        Assert.Empty(preferences.KeywordFilters);
    }

    [Fact]
    public void CreateDefault_SendsOnEnter_SoNobodyIsSilentlyMovedOffIt()
    {
        // Existing rows get this from the migration's column DEFAULT, and new ones from here.
        // Both must agree: a user who never expressed an opinion keeps the behaviour every
        // messenger trains, rather than landing in the modifier mode they never asked for.
        var preferences = UserPreferences.CreateDefault(Ulid.NewUlid(), Now);

        Assert.True(preferences.SendMessageOnEnter);
    }

    [Fact]
    public void UpdateComposer_TogglesTheSendKey()
    {
        var preferences = UserPreferences.CreateDefault(Ulid.NewUlid(), Now);

        preferences.UpdateComposer(false, Now.AddMinutes(5));
        Assert.False(preferences.SendMessageOnEnter);
        Assert.Equal(Now.AddMinutes(5), preferences.UpdatedAt);

        preferences.UpdateComposer(true, Now.AddMinutes(9));
        Assert.True(preferences.SendMessageOnEnter);
    }

    [Fact]
    public void UpdateComposer_DoesNotDisturbTheAccessibilityFlags()
    {
        // The two settings are stored together but describe different things; splitting the
        // mutators keeps a future edit to one from silently clearing the other.
        var preferences = UserPreferences.CreateDefault(Ulid.NewUlid(), Now);
        preferences.UpdateAccessibility(false, true, false, true, Now);

        preferences.UpdateComposer(false, Now.AddMinutes(1));

        Assert.False(preferences.HideFlashingMedia);
        Assert.True(preferences.ReduceMotion);
        Assert.False(preferences.DisableAutoplay);
        Assert.True(preferences.ReduceVisualNoise);
        Assert.False(preferences.SendMessageOnEnter);
    }
}
