using ErrorOr;

namespace FriendCore.Domain.Posts;

public static class PostErrors
{
    public const int MaxContentLength = 5000;

    // Not in the spec — a deliberate MVP cap (Twitter-style); raise the constant if needed.
    public const int MaxMediaPerPost = 4;

    public static Error NotFound =>
        Error.NotFound("Post.NotFound", "Post was not found.");

    // §5.3: Content is nullable so posts can be image-only — but never both empty.
    public static Error ContentOrMediaRequired =>
        Error.Validation("Post.ContentOrMediaRequired", "A post needs text content or at least one media item.");

    public static Error TooManyMedia =>
        Error.Validation("Post.TooManyMedia", $"A post can have at most {MaxMediaPerPost} media items.");

    public static Error DuplicateMedia =>
        Error.Validation("Post.DuplicateMedia", "The same media item cannot be attached twice.");

    public static Error TooManyTags =>
        Error.Validation("Post.TooManyTags",
            $"A post can have at most {Tags.TagLimits.MaxPostTags} tags.");

    public static Error DuplicateTags =>
        Error.Validation("Post.DuplicateTags", "The same tag cannot be attached twice.");

    public static Error ContentTooLong =>
        Error.Validation("Post.ContentTooLong", $"Post content must not exceed {MaxContentLength} characters.");

    public static Error NotAuthor =>
        Error.Forbidden("Post.NotAuthor", "You can only modify your own posts.");

    public static Error CommentNotFound =>
        Error.NotFound("Post.CommentNotFound", "Comment was not found.");

    public static Error CommentContentRequired =>
        Error.Validation("Post.CommentContentRequired", "Comment content is required.");

    public static Error CommentTooLong =>
        Error.Validation("Post.CommentTooLong", "Comment must not exceed 500 characters.");

    public static Error CommentNotAuthor =>
        Error.Forbidden("Post.CommentNotAuthor", "You can only delete your own comments.");
}
