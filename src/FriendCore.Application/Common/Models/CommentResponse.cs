using FriendCore.Domain.Posts;
using FriendCore.Domain.Users;

namespace FriendCore.Application.Common.Models;

public sealed record CommentResponse(
    string Id,
    string PostId,
    string AuthorId,
    string AuthorUsername,
    string AuthorDisplayName,
    string Content,
    DateTimeOffset CreatedAt)
{
    public static CommentResponse Create(Comment comment, User author) =>
        new(
            comment.Id.ToString(),
            comment.PostId.ToString(),
            comment.AuthorId.ToString(),
            author.Username,
            author.DisplayName,
            comment.Content,
            comment.CreatedAt);
}

public sealed record CommentFeedResponse(IReadOnlyList<CommentResponse> Comments, string? NextCursor);
