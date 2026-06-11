namespace FriendCore.Domain.Media;

/// <summary>
///     Bucket-relative object key layout (spec §7.1). Centralised so upload, processing, and
///     serving all compose identical keys; nothing else may hardcode these paths.
/// </summary>
public static class MediaStorageKeys
{
    public static string Original(Ulid uploaderId, Ulid mediaId, string fileExtension)
    {
        return $"uploads/originals/{uploaderId}/{mediaId}.{fileExtension}";
    }

    /// <summary>
    ///     Key for a generated WebP variant. Not valid for <see cref="MediaVariantType.Original" /> —
    ///     the original lives at <see cref="MediaItem.OriginalStorageKey" /> in its source format.
    /// </summary>
    public static string Variant(Ulid mediaId, MediaVariantType type)
    {
        if (type == MediaVariantType.Original)
            throw new ArgumentException(
                "The original is not a generated variant — use MediaItem.OriginalStorageKey.",
                nameof(type));

        return $"uploads/variants/{mediaId}/{type.ToString().ToLowerInvariant()}.webp";
    }
}
