using FriendCore.Domain.Media;

namespace FriendCore.Application.Common.Models;

public sealed record MediaVariantResponse(string Type, string Url, int WidthPx, int HeightPx);

/// <summary>
///     One media item attached to a post or a message. Only the public WebP variants appear — the original is
///     served exclusively via presigned URL (spec §7.3). The safety flags let clients honour the
///     §7.4 hide-flashing-media behaviour without an extra request.
/// </summary>
public sealed record MediaResponse(
    string Id,
    bool HasFlashingWarning,
    bool HasIntenseMotionWarning,
    IReadOnlyList<MediaVariantResponse> Variants)
{
    public static MediaResponse Create(MediaItem media, string? publicBaseUrl)
    {
        var variants = media.Variants
            .Where(variant => variant.Type != MediaVariantType.Original)
            .OrderBy(variant => variant.Type)
            .Select(variant => new MediaVariantResponse(
                variant.Type.ToString(),
                JoinUrl(publicBaseUrl, variant.StorageKey),
                variant.WidthPx,
                variant.HeightPx))
            .ToList();

        return new MediaResponse(
            media.Id.ToString(),
            media.HasFlashingWarning,
            media.HasIntenseMotionWarning,
            variants);
    }

    /// <summary>
    ///     Joins a configured base URL with a storage key. Plain interpolation once produced
    ///     "//bucket/..." paths (trailing slash in config), which S3 gateways reject with 400
    ///     while the API logs nothing — every image silently broken. Collapse repeats past
    ///     the scheme instead of trusting either side's slashes.
    /// </summary>
    internal static string JoinUrl(string? publicBaseUrl, string storageKey)
    {
        var combined = $"{(publicBaseUrl ?? string.Empty).TrimEnd('/')}/{storageKey.TrimStart('/')}";
        var schemeEnd = combined.IndexOf("://", StringComparison.Ordinal);
        if (schemeEnd < 0)
            return combined;

        var head = combined[..(schemeEnd + 3)];
        var rest = combined[(schemeEnd + 3)..];
        while (rest.Contains("//", StringComparison.Ordinal))
            rest = rest.Replace("//", "/", StringComparison.Ordinal);
        return head + rest;
    }
}
