using FriendCore.Application.Tags.Queries.GetTagAutocomplete;
using FriendCore.Application.Tags.Services;
using FriendCore.Domain.Tags;
using FriendCore.Infrastructure.Caching;
using NSubstitute;

namespace FriendCore.UnitTests.Tags;

public class GetTagAutocompleteQueryHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 2, 9, 0, 0, TimeSpan.Zero);

    private readonly ITagRepository _tagRepository = Substitute.For<ITagRepository>();

    private GetTagAutocompleteQueryHandler CreateHandler()
    {
        return new GetTagAutocompleteQueryHandler(_tagRepository, new TagNormalizationService(),
            new NullCacheService());
    }

    private static Tag ActiveTag(string name, string slug)
    {
        var tag = Tag.Create(name, slug, Ulid.NewUlid(), Now);
        tag.Approve();
        return tag;
    }

    [Fact]
    public async Task Handle_ReturnsEmpty_WhenPrefixNormalizesToNothing()
    {
        var result = await CreateHandler().Handle(new GetTagAutocompleteQuery("!!!"), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Empty(result.Value.Suggestions);
        Assert.False(result.Value.HasExactMatch);
        await _tagRepository.DidNotReceive()
            .SearchUsableByPrefixAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NormalizesPrefixAndMapsResults()
    {
        _tagRepository.SearchUsableByPrefixAsync("pho", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([ActiveTag("Photography", "photography"), ActiveTag("Photos", "photos")]);

        var result = await CreateHandler().Handle(new GetTagAutocompleteQuery("  Pho "), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal("pho", result.Value.Query);
        Assert.Equal(2, result.Value.Suggestions.Count);
        Assert.False(result.Value.HasExactMatch);
    }

    [Fact]
    public async Task Handle_SetsHasExactMatch_WhenSlugMatchesExactly()
    {
        _tagRepository.SearchUsableByPrefixAsync("photography", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([ActiveTag("Photography", "photography")]);

        var result = await CreateHandler().Handle(new GetTagAutocompleteQuery("Photography"), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.True(result.Value.HasExactMatch);
    }
}