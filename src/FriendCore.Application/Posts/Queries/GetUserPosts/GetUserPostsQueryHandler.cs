using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Application.Posts.Common;
using FriendCore.Domain.Posts;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Posts.Queries.GetUserPosts;

internal sealed class GetUserPostsQueryHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository,
    IPostRepository postRepository)
    : IRequestHandler<GetUserPostsQuery, ErrorOr<PostFeedResponse>>
{
    private const int PageSize = 20;

    public async Task<ErrorOr<PostFeedResponse>> Handle(GetUserPostsQuery request, CancellationToken cancellationToken)
    {
        var slug = User.ToUsernameSlug(request.Username);
        var user = await userRepository.GetByUsernameSlugAsync(slug, cancellationToken);
        if (user is null || user.IsBanned)
            return UserErrors.NotFound;

        var cursor = PostCursor.Decode(request.Cursor);

        var posts = await postRepository.GetByAuthorAsync(user.Id, cursor, PageSize + 1, cancellationToken);

        var hasMore = posts.Count > PageSize;
        var page = hasMore ? posts.Take(PageSize).ToList() : posts;

        var likedPostIds = await GetLikedPostIdsAsync(page, cancellationToken);

        var items = page
            .Select(post => PostResponse.Create(post, user, likedPostIds.Contains(post.Id)))
            .ToList();
        var nextCursor = hasMore ? PostCursor.Encode(page[^1].CreatedAt) : null;

        return new PostFeedResponse(items, nextCursor);
    }

    private async Task<HashSet<Ulid>> GetLikedPostIdsAsync(
        IReadOnlyList<Post> posts,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId || posts.Count == 0)
            return [];

        var ids = posts.Select(post => post.Id).ToList();
        var liked = await postRepository.GetLikedPostIdsAsync(userId, ids, cancellationToken);
        return liked.ToHashSet();
    }
}
