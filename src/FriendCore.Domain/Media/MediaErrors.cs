using ErrorOr;

namespace FriendCore.Domain.Media;

public static class MediaErrors
{
    // §7.1: API validates file size (max 20MB) and type (JPEG, PNG, WebP, GIF only).
    public const long MaxFileSizeBytes = 20L * 1024 * 1024;

    public static readonly IReadOnlySet<string> AllowedContentTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/gif"
    };

    public static Error NotFound =>
        Error.NotFound("Media.NotFound", "Media was not found.");

    public static Error HeaderMismatch =>
        Error.Validation("Media.HeaderMismatch", "File content does not match the declared image type.");

    public static Error NotOwner =>
        Error.Forbidden("Media.NotOwner", "You can only modify your own media.");

    public static Error NotReady =>
        Error.Validation("Media.NotReady", "Media is still processing and cannot be attached yet.");

    public static Error AlreadyAttached =>
        Error.Conflict("Media.AlreadyAttached", "Media is already attached to another post.");

    public static Error AttachedToPost =>
        Error.Conflict("Media.AttachedToPost", "Media attached to a post cannot be deleted — delete the post instead.");

    public static Error SafetyCategoryRequired =>
        Error.Validation("Media.SafetyCategoryRequired", "At least one safety category must be reported.");

    public static Error FileRequired =>
        Error.Validation("Media.FileRequired", "An image file is required.");

    public static Error UnsupportedType =>
        Error.Validation("Media.UnsupportedType", "Only JPEG, PNG, WebP, and GIF images are supported.");

    public static Error FileTooLarge =>
        Error.Validation("Media.FileTooLarge", "Image must not exceed 20MB.");
}
