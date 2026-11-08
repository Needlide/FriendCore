using FriendCore.Domain.Posts;
using FriendCore.Domain.Tags;
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
    DateTimeOffset UpdatedAt,
    IReadOnlyList<MediaResponse> Media,
    IReadOnlyList<PostTagResponse> Tags)
{
    public static PostResponse Create(
        Post post,
        User author,
        bool isLikedByCurrentUser,
        IReadOnlyList<MediaResponse> media,
        IReadOnlyList<PostTagResponse> tags) =>
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
            post.UpdatedAt,
            media,
            tags);
}

public sealed record PostTagResponse(string Name, string Slug)
{
    public static PostTagResponse Create(Tag tag) => new(tag.Name, tag.Slug);
}

public sealed record PostFeedResponse(IReadOnlyList<PostResponse> Posts, string? NextCursor);
