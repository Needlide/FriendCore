using ErrorOr;
using FriendCore.Domain.Common;
using FriendCore.Domain.Tags;

namespace FriendCore.Domain.Posts;

public sealed class Post : AggregateRoot<Ulid>, ISoftDeletable, IAuditable
{
    private readonly List<PostMedia> _media = [];
    private readonly List<PostTag> _tags = [];

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
    public IReadOnlyList<PostTag> Tags => _tags.AsReadOnly();

    // §5.3: content is nullable (image-only posts), but a post can never be empty of both text and
    // media. Media ownership/readiness checks are the handler's job (they need the repository),
    // as is tag name resolution; the count, duplicate, and content rules are invariants here.
    public static ErrorOr<Post> Create(
        Ulid authorId,
        string? content,
        ContentSensitivity sensitivity,
        DateTimeOffset createdAt,
        IReadOnlyList<Ulid>? mediaIds = null,
        IReadOnlyList<Ulid>? tagIds = null)
    {
        var trimmed = string.IsNullOrWhiteSpace(content) ? null : content.Trim();
        var media = mediaIds ?? [];
        var tags = tagIds ?? [];

        if (trimmed is null && media.Count == 0)
            return PostErrors.ContentOrMediaRequired;

        if (trimmed?.Length > PostErrors.MaxContentLength)
            return PostErrors.ContentTooLong;

        if (media.Count > PostErrors.MaxMediaPerPost)
            return PostErrors.TooManyMedia;

        if (media.Distinct().Count() != media.Count)
            return PostErrors.DuplicateMedia;

        if (tags.Count > TagLimits.MaxPostTags)
            return PostErrors.TooManyTags;

        if (tags.Distinct().Count() != tags.Count)
            return PostErrors.DuplicateTags;

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

        foreach (var tagId in tags)
            post._tags.Add(PostTag.Create(id, tagId, createdAt));

        return post;
    }

    // §5.3 MVP: text and sensitivity are editable, but media and tags are fixed at creation.
    // A text-only post cannot be edited down to nothing; an image-only post may have its
    // (optional) caption cleared while keeping its media.
    public ErrorOr<Success> Edit(string? content, ContentSensitivity sensitivity, DateTimeOffset updatedAt)
    {
        var trimmed = string.IsNullOrWhiteSpace(content) ? null : content.Trim();

        if (trimmed is null && Media.Count == 0)
            return PostErrors.ContentOrMediaRequired;

        if (trimmed?.Length > PostErrors.MaxContentLength)
            return PostErrors.ContentTooLong;

        Content = trimmed;
        Sensitivity = sensitivity;
        UpdatedAt = updatedAt;

        return Result.Success;
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
