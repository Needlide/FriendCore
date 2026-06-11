using FriendCore.Domain.Posts;
using FriendCore.Domain.Users;

namespace FriendCore.Application.Common.Models;

public sealed record PostResponse(
    string Id,
    string AuthorId,
    string AuthorUsername,
    string AuthorDisplayName,
    string? Content,
    string Sensitivity,
    int LikeCount,
    int CommentCount,
    bool IsLikedByCurrentUser,
    DateTimeOffset CreatedAt,
    IReadOnlyList<PostMediaResponse> Media)
{
    public static PostResponse Create(
        Post post,
        User author,
        bool isLikedByCurrentUser,
        IReadOnlyList<PostMediaResponse> media) =>
        new(
            post.Id.ToString(),
            post.AuthorId.ToString(),
            author.Username,
            author.DisplayName,
            post.Content,
            post.Sensitivity.ToString(),
            post.LikeCount,
            post.CommentCount,
            isLikedByCurrentUser,
            post.CreatedAt,
            media);
}

public sealed record PostFeedResponse(IReadOnlyList<PostResponse> Posts, string? NextCursor);
