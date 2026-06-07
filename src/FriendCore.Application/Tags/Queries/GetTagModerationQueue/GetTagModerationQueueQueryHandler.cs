using ErrorOr;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Tags;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Tags.Queries.GetTagModerationQueue;

internal sealed class GetTagModerationQueueQueryHandler(
    ITagRepository tagRepository,
    IUserRepository userRepository)
    : IRequestHandler<GetTagModerationQueueQuery, ErrorOr<TagModerationQueueResponse>>
{
    private const int MaxItems = 100;

    public async Task<ErrorOr<TagModerationQueueResponse>> Handle(
        GetTagModerationQueueQuery request,
        CancellationToken cancellationToken)
    {
        var tags = await tagRepository.GetByStatusAsync(TagStatus.PendingReview, MaxItems, cancellationToken);

        var creatorIds = tags.Select(tag => tag.CreatedById).Distinct().ToList();
        var creators = (await userRepository.GetByIdsAsync(creatorIds, cancellationToken))
            .ToDictionary(user => user.Id);

        var items = tags
            .Select(tag =>
            {
                creators.TryGetValue(tag.CreatedById, out var creator);
                return new TagModerationQueueItem(
                    tag.Id.ToString(),
                    tag.Name,
                    tag.Slug,
                    tag.CreatedAt,
                    tag.CreatedById.ToString(),
                    creator?.Username,
                    creator?.DisplayName);
            })
            .ToList();

        return new TagModerationQueueResponse(items);
    }
}
