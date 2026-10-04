using FriendCore.Application.Common.Models;
using FriendCore.Domain.Media;

namespace FriendCore.UnitTests.Media;

/// <summary>
/// Which hazard withholds a media item's URLs. Intense motion counts only for animated media —
/// image/gif is the sole animated format the API accepts (§7.1), so the flag on a still is noise.
/// </summary>
public class MediaGateReasonTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 9, 0, 0, TimeSpan.Zero);

    private static MediaItem Media(string contentType, bool flashing, bool motion)
    {
        var media = MediaItem.Create(
            Ulid.NewUlid(), "x.gif", contentType, ".gif", flashing, motion, Now);
        media.MarkReady(
            [MediaVariant.Create(MediaVariantType.Small, $"{media.Id}/small.webp", 320, 240, 1024)]);
        return media;
    }

    [Theory]
    [InlineData("image/gif", true, false, MediaGateReason.Flashing)]
    [InlineData("image/gif", false, true, MediaGateReason.IntenseMotion)]
    [InlineData("image/gif", true, true, MediaGateReason.FlashingAndIntenseMotion)]
    [InlineData("image/gif", false, false, MediaGateReason.None)]
    [InlineData("image/png", true, false, MediaGateReason.Flashing)]
    public void ClassifiesTheHazard(string contentType, bool flashing, bool motion, MediaGateReason expected)
    {
        Assert.Equal(expected, MediaResponse.GateReasonFor(Media(contentType, flashing, motion)));
    }

    [Theory]
    [InlineData("image/png")]
    [InlineData("image/jpeg")]
    [InlineData("image/webp")]
    public void IntenseMotionAloneDoesNotGateAStill(string contentType)
    {
        // The point of the change: a still image cannot contain movement. Gating one for
        // "intense motion" mis-filed a nuisance as a seizure hazard and left ReduceMotion with
        // nothing to do. GIF is the only animated format the API accepts (§7.1).
        Assert.Equal(MediaGateReason.None, MediaResponse.GateReasonFor(Media(contentType, false, true)));
        Assert.NotEmpty(MediaResponse.Create(Media(contentType, false, true), "https://cdn").Variants);
    }

    [Fact]
    public void ADeclaredStillIsStillGated_ByTheFlashingBranch()
    {
        // The single self-declaration checkbox sets BOTH flags, so ticking it on a JPEG gates —
        // readers are not left unprotected by a technicality about animation.
        var media = Media("image/jpeg", true, true);
        Assert.Equal(MediaGateReason.Flashing, MediaResponse.GateReasonFor(media));
        Assert.Empty(MediaResponse.Create(media, "https://cdn").Variants);
    }

    [Fact]
    public void GateReasonTravelsWithTheGatedResponse_SoTheClientDoesNotReDeriveIt()
    {
        var gated = MediaResponse.Create(Media("image/gif", false, true), "https://cdn");
        Assert.Equal("IntenseMotion", gated.GateReason);
        Assert.Empty(gated.Variants);

        var ungated = MediaResponse.Create(Media("image/png", false, false), "https://cdn");
        // Null, not "None": absence is the common case and reads as falsy on the client.
        Assert.Null(ungated.GateReason);
        Assert.NotEmpty(ungated.Variants);
    }

    [Fact]
    public void ConsentBypassesTheGate_ButTheReasonIsStillReported()
    {
        var media = Media("image/gif", true, true);
        var granted = MediaResponse.Create(media, "https://cdn", includeVariants: true);
        Assert.NotEmpty(granted.Variants);
        // The reason survives so the client can keep explaining what the reader agreed to.
        Assert.Equal("FlashingAndIntenseMotion", granted.GateReason);
    }
}
