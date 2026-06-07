using ErrorOr;
using FriendCore.Domain.Common;

namespace FriendCore.Domain.Posts;

public sealed class Post : AggregateRoot<Ulid>, ISoftDeletable, IAuditable
{
    private Post(Ulid id, DateTimeOffset createdAt)
        : base(id, createdAt)
    {
    }

    private Post()
    {
    }

    public Ulid AuthorId { get; private set; }
    public string? Content { get; private set; }
    public ContentSensitivity Sensitivity { get; private set; }
    public int LikeCount { get; private set; }
    public int CommentCount { get; private set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    // MVP is text-only (media deferred), so content is required even though the column is nullable
    // to allow future image-only posts without a schema change.
    public static ErrorOr<Post> Create(
        Ulid authorId,
        string? content,
        ContentSensitivity sensitivity,
        DateTimeOffset createdAt)
    {
        var trimmed = content?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return PostErrors.ContentRequired;

        if (trimmed.Length > PostErrors.MaxContentLength)
            return PostErrors.ContentTooLong;

        return new Post(Ulid.NewUlid(), createdAt)
        {
            AuthorId = authorId,
            Content = trimmed,
            Sensitivity = sensitivity,
            UpdatedAt = createdAt
        };
    }

    public void IncrementLikeCount() => LikeCount++;

    public void DecrementLikeCount()
    {
        if (LikeCount > 0)
            LikeCount--;
    }

    public void IncrementCommentCount() => CommentCount++;

    public void DecrementCommentCount()
    {
        if (CommentCount > 0)
            CommentCount--;
    }
}
