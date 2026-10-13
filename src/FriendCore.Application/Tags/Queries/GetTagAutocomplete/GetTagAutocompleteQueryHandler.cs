using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Application.Tags.Services;
using FriendCore.Domain.Tags;
using MediatR;

namespace FriendCore.Application.Tags.Queries.GetTagAutocomplete;

internal sealed class GetTagAutocompleteQueryHandler(
    ITagRepository tagRepository,
    ITagNormalizer normalizer,
    ICacheService cache)
    : IRequestHandler<GetTagAutocompleteQuery, ErrorOr<TagAutocompleteResponse>>
{
    private const int MaxResults = 10;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public async Task<ErrorOr<TagAutocompleteResponse>> Handle(
        GetTagAutocompleteQuery request,
        CancellationToken cancellationToken)
    {
        var slug = normalizer.ToSlug(request.Query);
        if (slug.Length == 0)
            return new TagAutocompleteResponse(slug, [], false);

        return await cache.GetOrSetAsync(
            $"tags:autocomplete:{slug}",
            async token =>
            {
                var tags = await tagRepository.SearchUsableByPrefixAsync(slug, MaxResults, token);
                var suggestions = tags
                    .Select(tag => new TagSuggestion(tag.Id.ToString(), tag.Name, tag.Slug, tag.TotalCount))
                    .ToList();

                return new TagAutocompleteResponse(
                    slug,
                    suggestions,
                    suggestions.Any(suggestion => suggestion.Slug == slug));
            },
            CacheTtl,
            cancellationToken);
    }
}