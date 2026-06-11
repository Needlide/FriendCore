using FriendCore.Domain.Media;
using FriendCore.Infrastructure.MediaProcessing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace FriendCore.UnitTests.Media;

public class MediaVariantPlannerTests
{
    private static Size SizeFor(int width, int height, MediaVariantType type)
    {
        return MediaVariantPlanner.Plan(width, height).Single(p => p.Type == type).Size;
    }

    [Fact]
    public void Plan_LargeSource_UsesFullTargetSizes()
    {
        var plan = MediaVariantPlanner.Plan(2000, 1500);

        Assert.Equal(4, plan.Count);
        Assert.Equal(new Size(100, 100), SizeFor(2000, 1500, MediaVariantType.Thumbnail));
        Assert.Equal(new Size(300, 0), SizeFor(2000, 1500, MediaVariantType.Small));
        Assert.Equal(new Size(800, 0), SizeFor(2000, 1500, MediaVariantType.Medium));
        Assert.Equal(new Size(1400, 0), SizeFor(2000, 1500, MediaVariantType.Large));
    }

    [Fact]
    public void Plan_Thumbnail_IsSquareCrop()
    {
        var (_, size, mode) = MediaVariantPlanner.Plan(2000, 1500).Single(p => p.Type == MediaVariantType.Thumbnail);

        Assert.Equal(ResizeMode.Crop, mode);
        Assert.Equal(size.Width, size.Height);
    }

    [Fact]
    public void Plan_SmallSource_NeverUpscales()
    {
        // 80×60 source: every width target caps at 80; the square thumbnail caps at the shorter side.
        Assert.Equal(new Size(60, 60), SizeFor(80, 60, MediaVariantType.Thumbnail));
        Assert.Equal(new Size(80, 0), SizeFor(80, 60, MediaVariantType.Small));
        Assert.Equal(new Size(80, 0), SizeFor(80, 60, MediaVariantType.Medium));
        Assert.Equal(new Size(80, 0), SizeFor(80, 60, MediaVariantType.Large));
    }

    [Fact]
    public void Plan_PortraitSource_CapsAtSourceWidth()
    {
        // 600×1200 portrait: width caps bind on width, not the longer side.
        Assert.Equal(new Size(100, 100), SizeFor(600, 1200, MediaVariantType.Thumbnail));
        Assert.Equal(new Size(300, 0), SizeFor(600, 1200, MediaVariantType.Small));
        Assert.Equal(new Size(600, 0), SizeFor(600, 1200, MediaVariantType.Medium));
        Assert.Equal(new Size(600, 0), SizeFor(600, 1200, MediaVariantType.Large));
    }

    [Fact]
    public void Plan_DoesNotIncludeOriginal()
    {
        Assert.DoesNotContain(
            MediaVariantPlanner.Plan(500, 500),
            p => p.Type == MediaVariantType.Original);
    }
}
