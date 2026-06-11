using FriendCore.Domain.Media;

namespace FriendCore.Application.Media;

/// <summary>
///     The allowed image formats (spec §7.1): declared-type checks, magic-byte verification, and
///     the storage file extension per content type.
/// </summary>
public static class ImageContentTypes
{
    /// <summary>Bytes needed to verify any supported signature (WebP needs RIFF@0 + WEBP@8).</summary>
    public const int HeaderLength = 12;

    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] GifSignature = "GIF8"u8.ToArray();
    private static readonly byte[] RiffSignature = "RIFF"u8.ToArray();
    private static readonly byte[] WebpSignature = "WEBP"u8.ToArray();

    public static bool IsAllowed(string contentType)
    {
        return MediaErrors.AllowedContentTypes.Contains(contentType);
    }

    /// <summary>Storage file extension for an allowed content type (validate first).</summary>
    public static string Extension(string contentType)
    {
        return contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => "jpg",
            "image/png" => "png",
            "image/webp" => "webp",
            "image/gif" => "gif",
            _ => throw new ArgumentException($"Unsupported content type '{contentType}'.", nameof(contentType))
        };
    }

    /// <summary>Verifies the file header matches the declared content type (spec §7.1 step 2).</summary>
    public static bool HeaderMatches(string contentType, ReadOnlySpan<byte> header)
    {
        return contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => header.StartsWith(JpegSignature),
            "image/png" => header.StartsWith(PngSignature),
            "image/gif" => header.StartsWith(GifSignature),
            "image/webp" => header.Length >= HeaderLength &&
                            header.StartsWith(RiffSignature) &&
                            header.Slice(8, 4).SequenceEqual(WebpSignature),
            _ => false
        };
    }
}
