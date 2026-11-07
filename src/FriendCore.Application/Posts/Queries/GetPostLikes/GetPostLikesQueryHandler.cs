using ErrorOr;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Posts;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Posts.Queries.GetPostLikes;

internal sealed class GetPostLikesQueryHandler(
    IPostRepository postRepository,
    IUserRepository userRepository)
    : IRequestHandler<GetPostLikesQuery, ErrorOr<LikeListResponse>>
{
    // Same generous cap as follower/following and the safety lists. The UI is a dialog, not a
    // paginated screen; a post with more than 200 likes still shows the most recent 200.
    private const int MaxResults = 200;

    public async Task<ErrorOr<LikeListResponse>> Handle(
        GetPostLikesQuery request,
        CancellationToken cancellationToken)
    {
        // A block-hidden or deleted post is a 404 here too, exactly like GET /posts/{id}.
        var post = await postRepository.GetByIdAsync(request.PostId, cancellationToken);
        if (post is null)
            return PostErrors.NotFound;

        var likerIds = await postRepository.GetLikerIdsAsync(post.Id, MaxResults, cancellationToken);
        if (likerIds.Count == 0)
            return new LikeListResponse([]);

        // Likes are deliberately not block-filtered (counts may include blocked users), so the
        // user lookup is what makes a blocked liker disappear from this list.
        var users = (await userRepository.GetByIdsAsync(likerIds, cancellationToken))
            .ToDictionary(user => user.Id);

        var summaries = likerIds
            .Where(users.ContainsKey)
            .Select(id => UserSummaryResponse.FromUser(users[id]))
            .ToList();

        return new LikeListResponse(summaries);
    }
}
