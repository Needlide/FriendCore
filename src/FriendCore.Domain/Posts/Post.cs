using ErrorOr;
using FriendCore.Domain.Common;

namespace FriendCore.Domain.Posts;

public sealed class Post : AggregateRoot<Ulid>, ISoftDeletable, IAuditable
{
    private readonly List<PostMedia> _media = [];

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

    public IReadOnlyList<PostMedia> Media => _media.AsReadOnly();

    // §5.3: content is nullable (image-only posts), but a post can never be empty of both text and
    // media. Media ownership/readiness checks are the handler's job (they need the repository);
    // the count, duplicate, and content rules are invariants enforced here.
    public static ErrorOr<Post> Create(
        Ulid authorId,
        string? content,
        ContentSensitivity sensitivity,
        DateTimeOffset createdAt,
        IReadOnlyList<Ulid>? mediaIds = null)
    {
        var trimmed = string.IsNullOrWhiteSpace(content) ? null : content.Trim();
        var media = mediaIds ?? [];

        if (trimmed is null && media.Count == 0)
            return PostErrors.ContentOrMediaRequired;

        if (trimmed?.Length > PostErrors.MaxContentLength)
            return PostErrors.ContentTooLong;

        if (media.Count > PostErrors.MaxMediaPerPost)
            return PostErrors.TooManyMedia;

        if (media.Distinct().Count() != media.Count)
            return PostErrors.DuplicateMedia;

        var id = Ulid.NewUlid();
        var post = new Post(id, createdAt)
        {
            AuthorId = authorId,
            Content = trimmed,
            Sensitivity = sensitivity,
            UpdatedAt = createdAt
        };

        // DisplayOrder follows the order the client sent the ids in.
        for (var order = 0; order < media.Count; order++)
            post._media.Add(PostMedia.Create(id, media[order], order));

        return post;
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
