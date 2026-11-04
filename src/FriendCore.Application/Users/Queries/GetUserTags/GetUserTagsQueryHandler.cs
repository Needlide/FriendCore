using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Users;
using FriendCore.Domain.Common;
using FriendCore.Domain.Tags;
using MediatR;

namespace FriendCore.Application.Users.Queries.GetUserTags;

internal sealed class GetUserTagsQueryHandler : IRequestHandler<GetUserTagsQuery, ErrorOr<UserTagsResponse>>
{
    private readonly IUserRepository _userRepository;
    private readonly ITagRepository _tagRepository;
    private readonly ICurrentUserService _currentUserService;

    public GetUserTagsQueryHandler(IUserRepository userRepository, ITagRepository tagRepository, ICurrentUserService currentUserService)
    {
        _userRepository = userRepository;
        _tagRepository = tagRepository;
        _currentUserService = currentUserService;
    }

    public async Task<ErrorOr<UserTagsResponse>> Handle(GetUserTagsQuery request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByUsernameSlugAsync(request.Username, cancellationToken);
        if (user is null)
            return UserErrors.NotFound;

        if (user.ProfileTags.Count == 0)
            return new UserTagsResponse([]);

        var tags = await _tagRepository.GetByIdsAsync(
            user.ProfileTags.Select(pt => pt.TagId).Distinct().ToList(), cancellationToken);
        var byId = tags.ToDictionary(t => t.Id);

        var items = user.ProfileTags
            .Where(pt => byId.ContainsKey(pt.TagId))
            .Select(pt =>
            {
                var tag = byId[pt.TagId];
                return new ProfileTagItem(
                    tag.Id.ToString(), tag.Name, tag.Slug, pt.Tier.ToString(), pt.IsPublic);
            })
            .ToList();

        return new UserTagsResponse(items.Select(i => i.Name).ToList());
    }
}
