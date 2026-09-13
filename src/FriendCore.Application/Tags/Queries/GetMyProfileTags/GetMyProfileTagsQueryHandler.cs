using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Common;
using FriendCore.Domain.Tags;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Tags.Queries.GetMyProfileTags;

internal sealed class GetMyProfileTagsQueryHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository,
    ITagRepository tagRepository)
    : IRequestHandler<GetMyProfileTagsQuery, ErrorOr<ProfileTagsResponse>>
{
    public async Task<ErrorOr<ProfileTagsResponse>> Handle(
        GetMyProfileTagsQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return CommonErrors.Unauthorized;

        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
            return UserErrors.NotFound;

        if (user.ProfileTags.Count == 0)
            return new ProfileTagsResponse([]);

        var tags = await tagRepository.GetByIdsAsync(
            user.ProfileTags.Select(t => t.TagId).Distinct().ToList(), cancellationToken);
        var byId = tags.ToDictionary(t => t.Id);

        // A merged-away tag id may no longer resolve — skip silently, never 500.
        var items = user.ProfileTags
            .Where(pt => byId.ContainsKey(pt.TagId))
            .Select(pt =>
            {
                var tag = byId[pt.TagId];
                return new ProfileTagItem(
                    tag.Id.ToString(), tag.Name, tag.Slug, pt.Tier.ToString(), pt.IsPublic);
            })
            .ToList();

        return new ProfileTagsResponse(items);
    }
}
