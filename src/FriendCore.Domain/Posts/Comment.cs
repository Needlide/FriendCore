using ErrorOr;
using FriendCore.Domain.Common;

namespace FriendCore.Domain.Posts;

public sealed class Comment : Entity<Ulid>, ISoftDeletable
{
    public const int MaxContentLength = 500;

    private Comment(Ulid id, DateTimeOffset createdAt)
        : base(id, createdAt)
    {
    }

    private Comment()
    {
    }

    public Ulid PostId { get; private set; }
    public Ulid AuthorId { get; private set; }
    public string Content { get; private set; } = null!;
    public DateTimeOffset? EditedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public static ErrorOr<Comment> Create(Ulid postId, Ulid authorId, string? content, DateTimeOffset createdAt)
    {
        var trimmed = content?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return PostErrors.CommentContentRequired;

        if (trimmed.Length > MaxContentLength)
            return PostErrors.CommentTooLong;

        return new Comment(Ulid.NewUlid(), createdAt)
        {
            PostId = postId,
            AuthorId = authorId,
            Content = trimmed
        };
    }

    public ErrorOr<Success> Edit(string? content, DateTimeOffset at)
    {
        var trimmed = content?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return PostErrors.CommentContentRequired;

        if (trimmed.Length > MaxContentLength)
            return PostErrors.CommentTooLong;

        Content = trimmed;
        // Keeps the FIRST edit time: "edited" is a fact about the comment, not a clock that
        // resets every time the author fixes a typo (matches Message.Edit).
        EditedAt ??= at;

        return Result.Success;
    }
}
