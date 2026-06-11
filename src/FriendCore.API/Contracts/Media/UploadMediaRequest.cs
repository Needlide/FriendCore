namespace FriendCore.API.Contracts.Media;

/// <summary>
///     Multipart form upload. The flashing/intense-motion booleans are the creator
///     self-declaration checkboxes (spec §7.4 layer 1).
/// </summary>
public sealed record UploadMediaRequest(
    IFormFile? File,
    bool HasFlashingWarning,
    bool HasIntenseMotionWarning);
