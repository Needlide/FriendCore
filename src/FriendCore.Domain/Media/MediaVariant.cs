namespace FriendCore.Domain.Media;

// Value object owned by MediaItem: one processed rendition (a WebP variant, or the preserved
// original). Width/height/size describe the stored file. StorageKey is the bucket-relative
// object key — never a full URL, so a storage/CDN move is config-only (no data migration).
public sealed class MediaVariant
{
    private MediaVariant()
    {
    }

    public MediaVariantType Type { get; private set; }
    public string StorageKey { get; private set; } = null!;
    public int WidthPx { get; private set; }
    public int HeightPx { get; private set; }
    public long FileSizeBytes { get; private set; }

    public static MediaVariant Create(
        MediaVariantType type,
        string storageKey,
        int widthPx,
        int heightPx,
        long fileSizeBytes)
    {
        return new MediaVariant
        {
            Type = type,
            StorageKey = storageKey,
            WidthPx = widthPx,
            HeightPx = heightPx,
            FileSizeBytes = fileSizeBytes
        };
    }
}
