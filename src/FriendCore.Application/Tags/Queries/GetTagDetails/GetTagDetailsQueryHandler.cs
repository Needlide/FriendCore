using ErrorOr;
using FriendCore.Application.Common.Models;
using FriendCore.Application.Tags.Services;
using FriendCore.Domain.Tags;
using MediatR;

namespace FriendCore.Application.Tags.Queries.GetTagDetails;

internal sealed class GetTagDetailsQueryHandler(
    ITagRepository tagRepository,
    ITagNormalizer normalizer)
    : IRequestHandler<GetTagDetailsQuery, ErrorOr<TagResponse>>
{
    public async Task<ErrorOr<TagResponse>> Handle(GetTagDetailsQuery request, CancellationToken cancellationToken)
    {
        var slug = normalizer.ToSlug(request.Slug);

        var tag = await tagRepository.GetBySlugAsync(slug, cancellationToken);
        if (tag is null)
            return TagErrors.NotFound;

        return TagResponse.FromTag(tag);
    }
}