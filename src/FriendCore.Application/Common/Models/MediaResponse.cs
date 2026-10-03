using FriendCore.Domain.Media;

namespace FriendCore.Application.Common.Models;

public sealed record MediaVariantResponse(string Type, string Url, int WidthPx, int HeightPx);

/// <summary>
///     One media item attached to a post or a message. Only the public WebP variants appear — the original is
///     served exclusively via presigned URL (spec §7.3).
/// </summary>
/// <remarks>
///     FLAGGED MEDIA CARRIES NO URLS. <see cref="Create" /> withholds the variant list for anything
///     declared or flagged as flashing / intense-motion (§7.4), on every surface, unconditionally.
///     A client gets the flags and an empty <c>Variants</c>, and must call
///     <c>POST /media/{id}/reveal</c> to obtain the URLs — which is authorized server-side against
///     whatever the media is actually attached to.
/// <para>
///     This is the single point of truth on purpose. It used to be the client: the flags shipped in
///     the payload and each surface re-derived "should I show a placeholder?" from them, so any
///     future surface that forgot would silently serve the image — and nothing would catch it. Making
///     the server withhold the URL means a missed check degrades to a broken image, not to a
///     photosensitivity hazard.
/// <para>
///     What it does NOT do: revoke access. Variant URLs are public and unsigned
///     (<c>StorageOptions.PublicBaseUrl</c> + key), so anyone who already holds one keeps it. The
///     guarantee is that the API never <em>issues</em> a flagged image's location without consent —
///     not that the bytes are unreachable. Closing that gap means signed variant URLs or a proxy,
///     both of which are a storage-layer project, not an API one.
/// </remarks>
public sealed record MediaResponse(
    string Id,
    bool HasFlashingWarning,
    bool HasIntenseMotionWarning,
    IReadOnlyList<MediaVariantResponse> Variants)
{
    /// <summary>
    /// Builds a media payload for a list response, applying the flagged-media gate.
    /// </summary>
    public static MediaResponse Create(MediaItem media, string? publicBaseUrl) =>
        Create(media, publicBaseUrl, includeVariants: false);

    /// <summary>
    /// The gate is the default. <paramref name="includeVariants" /> exists for exactly one caller —
    /// the authorized consent endpoint — and is never reachable from a list response.
    /// </summary>
    public static MediaResponse Create(MediaItem media, string? publicBaseUrl, bool includeVariants)
    {
        // Everything else about the record stays the same, so a client can still tell the reader
        // *why* it is seeing no image.
        if (!includeVariants && (media.HasFlashingWarning || media.HasIntenseMotionWarning))
            return new MediaResponse(
                media.Id.ToString(),
                media.HasFlashingWarning,
                media.HasIntenseMotionWarning,
                []);

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
