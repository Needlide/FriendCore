using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Application.Posts.Common;
using FriendCore.Domain.Common;
using FriendCore.Domain.Posts;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Posts.Queries.GetHomeFeed;

internal sealed class GetHomeFeedQueryHandler(
    ICurrentUserService currentUserService,
    IPostRepository postRepository,
    IUserRepository userRepository)
    : IRequestHandler<GetHomeFeedQuery, ErrorOr<PostFeedResponse>>
{
    private const int PageSize = 20;

    public async Task<ErrorOr<PostFeedResponse>> Handle(GetHomeFeedQuery request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return CommonErrors.Unauthorized;

        var cursor = PostCursor.Decode(request.Cursor);
        var posts = await postRepository.GetHomeFeedAsync(userId, cursor, PageSize + 1, cancellationToken);

        var hasMore = posts.Count > PageSize;
        var page = hasMore ? posts.Take(PageSize).ToList() : posts;

        var authorIds = page.Select(post => post.AuthorId).Distinct().ToList();
        var authors = (await userRepository.GetByIdsAsync(authorIds, cancellationToken))
            .ToDictionary(user => user.Id);

        var likedPostIds = page.Count == 0
            ? []
            : (await postRepository.GetLikedPostIdsAsync(userId, page.Select(post => post.Id).ToList(),
                cancellationToken))
            .ToHashSet();

        var items = page
            .Where(post => authors.ContainsKey(post.AuthorId))
            .Select(post => PostResponse.Create(post, authors[post.AuthorId], likedPostIds.Contains(post.Id)))
            .ToList();

        var nextCursor = hasMore ? PostCursor.Encode(page[^1].CreatedAt, page[^1].Id) : null;
        return new PostFeedResponse(items, nextCursor);
    }
}