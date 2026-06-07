using ErrorOr;

namespace FriendCore.Domain.Posts;

public static class PostErrors
{
    public const int MaxContentLength = 5000;

    public static Error NotFound =>
        Error.NotFound("Post.NotFound", "Post was not found.");

    public static Error ContentRequired =>
        Error.Validation("Post.ContentRequired", "Post content is required.");

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
