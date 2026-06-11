using FriendCore.Domain.Media;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace FriendCore.Infrastructure.MediaProcessing;

/// <summary>
///     Target sizes for the spec §7.1 variants, honouring the no-upscale rule: the thumbnail is a
///     centre-crop to a square (capped at 100px and at the source's shorter side); the width
///     variants cap at the source width and preserve aspect ratio (height 0 = auto).
/// </summary>
public static class MediaVariantPlanner
{
    private const int ThumbnailSide = 100;
    private const int SmallWidth = 300;
    private const int MediumWidth = 800;
    private const int LargeWidth = 1400;

    public static IReadOnlyList<(MediaVariantType Type, Size Size, ResizeMode Mode)> Plan(int width, int height)
    {
        var thumbnailSide = Math.Min(ThumbnailSide, Math.Min(width, height));

        return
        [
            (MediaVariantType.Thumbnail, new Size(thumbnailSide, thumbnailSide), ResizeMode.Crop),
            (MediaVariantType.Small, new Size(Math.Min(SmallWidth, width), 0), ResizeMode.Max),
            (MediaVariantType.Medium, new Size(Math.Min(MediumWidth, width), 0), ResizeMode.Max),
            (MediaVariantType.Large, new Size(Math.Min(LargeWidth, width), 0), ResizeMode.Max)
        ];
    }
}
