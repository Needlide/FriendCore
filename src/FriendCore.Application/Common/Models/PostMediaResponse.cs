using FriendCore.Domain.Media;

namespace FriendCore.Application.Common.Models;

public sealed record MediaVariantResponse(string Type, string Url, int WidthPx, int HeightPx);

/// <summary>
///     One media item attached to a post. Only the public WebP variants appear — the original is
///     served exclusively via presigned URL (spec §7.3). The safety flags let clients honour the
///     §7.4 hide-flashing-media behaviour without an extra request.
/// </summary>
public sealed record PostMediaResponse(
    string Id,
    bool HasFlashingWarning,
    bool HasIntenseMotionWarning,
    IReadOnlyList<MediaVariantResponse> Variants)
{
    public static PostMediaResponse Create(MediaItem media, string? publicBaseUrl)
    {
        var variants = media.Variants
            .Where(variant => variant.Type != MediaVariantType.Original)
            .OrderBy(variant => variant.Type)
            .Select(variant => new MediaVariantResponse(
                variant.Type.ToString(),
                $"{publicBaseUrl}/{variant.StorageKey}",
                variant.WidthPx,
                variant.HeightPx))
            .ToList();

        return new PostMediaResponse(
            media.Id.ToString(),
            media.HasFlashingWarning,
            media.HasIntenseMotionWarning,
            variants);
    }
}
