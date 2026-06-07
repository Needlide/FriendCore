using FriendCore.Application.Tags.Services;

namespace FriendCore.UnitTests.Tags;

public class TagNormalizationServiceTests
{
    private readonly TagNormalizationService _normalizer = new();

    [Theory]
    [InlineData("Photography", "photography")]
    [InlineData("  Indie Game Dev  ", "indie-game-dev")]
    [InlineData("café", "cafe")]
    [InlineData("Sci-Fi", "sci-fi")]
    [InlineData("rock_and_roll", "rock-and-roll")]
    [InlineData("hello   world", "hello-world")]
    [InlineData("--trim--me--", "trim-me")]
    public void Normalize_ProducesExpectedSlug(string input, string expectedSlug)
    {
        var result = _normalizer.Normalize(input);

        Assert.False(result.IsError);
        Assert.Equal(expectedSlug, result.Value.Slug);
    }

    [Fact]
    public void Normalize_PreservesOriginalCasingInDisplayName()
    {
        var result = _normalizer.Normalize("  Indie Game Dev  ");

        Assert.False(result.IsError);
        Assert.Equal("Indie Game Dev", result.Value.DisplayName);
        Assert.Equal("indie-game-dev", result.Value.Slug);
    }

    [Theory]
    [InlineData("a")]          // one usable char -> too short
    [InlineData("C++")]        // symbols stripped -> "c" -> too short (symbol mapping deferred)
    [InlineData("!!!")]        // nothing usable -> empty
    [InlineData("   ")]        // whitespace only -> empty
    public void Normalize_RejectsInputThatNormalizesTooShort(string input)
    {
        var result = _normalizer.Normalize(input);

        Assert.True(result.IsError);
        Assert.StartsWith("Tag.", result.FirstError.Code);
    }

    [Fact]
    public void Normalize_RejectsTooLongSlug()
    {
        var result = _normalizer.Normalize(new string('a', 51));

        Assert.True(result.IsError);
        Assert.Equal("Tag.TooLong", result.FirstError.Code);
    }

    [Fact]
    public void ToSlug_MatchesNormalizeForSearchPrefixes()
    {
        Assert.Equal("pho", _normalizer.ToSlug("Pho"));
        Assert.Equal("indie-g", _normalizer.ToSlug("Indie G"));
    }
}
