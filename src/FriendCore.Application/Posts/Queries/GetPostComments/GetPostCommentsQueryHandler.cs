using ErrorOr;
using FriendCore.Application.Common.Models;
using FriendCore.Application.Posts.Common;
using FriendCore.Domain.Posts;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Posts.Queries.GetPostComments;

internal sealed class GetPostCommentsQueryHandler(
    IPostRepository postRepository,
    IUserRepository userRepository)
    : IRequestHandler<GetPostCommentsQuery, ErrorOr<CommentFeedResponse>>
{
    private const int PageSize = 20;

    public async Task<ErrorOr<CommentFeedResponse>> Handle(
        GetPostCommentsQuery request,
        CancellationToken cancellationToken)
    {
        var post = await postRepository.GetByIdAsync(request.PostId, cancellationToken);
        if (post is null)
            return PostErrors.NotFound;

        var cursor = PostCursor.Decode(request.Cursor);
        var comments = await postRepository.GetCommentsByPostAsync(post.Id, cursor, PageSize + 1, cancellationToken);

        var hasMore = comments.Count > PageSize;
        var page = hasMore ? comments.Take(PageSize).ToList() : comments;

        var authorIds = page.Select(comment => comment.AuthorId).Distinct().ToList();
        var authors = (await userRepository.GetByIdsAsync(authorIds, cancellationToken))
            .ToDictionary(user => user.Id);

        var items = page
            .Where(comment => authors.ContainsKey(comment.AuthorId))
            .Select(comment => CommentResponse.Create(comment, authors[comment.AuthorId]))
            .ToList();

        var nextCursor = hasMore ? PostCursor.Encode(page[^1].CreatedAt, page[^1].Id) : null;
        return new CommentFeedResponse(items, nextCursor);
    }
}