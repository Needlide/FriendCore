using FriendCore.Domain.Media;

namespace FriendCore.UnitTests.Media;

public class MediaItemTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 11, 9, 0, 0, TimeSpan.Zero);

    private static MediaItem CreateItem(bool flashing = false, bool intenseMotion = false)
    {
        return MediaItem.Create(
            Ulid.NewUlid(), "photo.jpg", "image/jpeg", "jpg",
            MediaSafetyFlags.From(
                ("flashing", flashing),
                ("intenseMotion", intenseMotion)),
            Now);
    }

    private static MediaVariant Variant(MediaVariantType type = MediaVariantType.Thumbnail)
    {
        return MediaVariant.Create(type, $"uploads/variants/x/{type}.webp", 100, 100, 1234);
    }

    [Fact]
    public void Create_StartsProcessing_AndComposesOriginalStorageKey()
    {
        var uploaderId = Ulid.NewUlid();

        var media = MediaItem.Create(
            uploaderId, "photo.jpg", "image/jpeg", "jpg", MediaSafetyFlags.Empty, Now);

        Assert.Equal(MediaStatus.Processing, media.Status);
        Assert.False(media.IsReady);
        Assert.Equal($"uploads/originals/{uploaderId}/{media.Id}.jpg", media.OriginalStorageKey);
        Assert.Equal(MediaSafetySource.None, media.SafetySource);
        Assert.Null(media.SafetyFlaggedAt);
        Assert.Empty(media.Variants);
    }

    [Fact]
    public void Create_WithSelfDeclaredWarning_RecordsUserSelfMarkedSource()
    {
        var media = CreateItem(flashing: true);

        Assert.True(media.SafetyFlags.IsSet("flashing"));
        Assert.False(media.SafetyFlags.IsSet("intenseMotion"));
        Assert.Equal(MediaSafetySource.UserSelfMarked, media.SafetySource);
        Assert.Equal(Now, media.SafetyFlaggedAt);
    }

    [Fact]
    public void MarkReady_PopulatesVariants_AndSetsReady()
    {
        var media = CreateItem();

        media.MarkReady([Variant(MediaVariantType.Thumbnail), Variant(MediaVariantType.Medium)]);

        Assert.True(media.IsReady);
        Assert.Equal(2, media.Variants.Count);
    }

    [Fact]
    public void MarkReady_IsNoOp_WhenAlreadyFinalised()
    {
        var media = CreateItem();
        media.MarkFailed();

        media.MarkReady([Variant()]);

        Assert.Equal(MediaStatus.Failed, media.Status);
        Assert.Empty(media.Variants);
    }

    [Fact]
    public void MarkFailed_IsNoOp_WhenAlreadyReady()
    {
        var media = CreateItem();
        media.MarkReady([Variant()]);

        media.MarkFailed();

        Assert.Equal(MediaStatus.Ready, media.Status);
    }

    [Fact]
    public void FlagSafety_IsAdditive_AndNeverClearsWarnings()
    {
        var media = CreateItem(flashing: true);

        media.FlagSafety(
            MediaSafetyFlags.From(("flashing", false), ("intenseMotion", true)),
            MediaSafetySource.UserReports,
            Now.AddHours(1));

        Assert.True(media.SafetyFlags.IsSet("flashing"));
        Assert.True(media.SafetyFlags.IsSet("intenseMotion"));
        Assert.Equal(MediaSafetySource.UserReports, media.SafetySource);
        Assert.Equal(Now.AddHours(1), media.SafetyFlaggedAt);
    }

    [Fact]
    public void FlagSafety_ModeratorDecision_IsNotOverwrittenByLaterReports()
    {
        var media = CreateItem();
        media.FlagSafety(
            MediaSafetyFlags.From(("flashing", true)),
            MediaSafetySource.Moderator,
            Now);

        media.FlagSafety(
            MediaSafetyFlags.From(("intenseMotion", true)),
            MediaSafetySource.UserReports,
            Now.AddHours(1));

        Assert.Equal(MediaSafetySource.Moderator, media.SafetySource);
        Assert.True(media.SafetyFlags.IsSet("intenseMotion"));
    }

    [Fact]
    public void StorageKeys_Variant_UsesLowercaseWebpLayout()
    {
        var mediaId = Ulid.NewUlid();

        Assert.Equal(
            $"uploads/variants/{mediaId}/thumbnail.webp",
            MediaStorageKeys.Variant(mediaId, MediaVariantType.Thumbnail));
    }

    [Fact]
    public void StorageKeys_Variant_RejectsOriginal()
    {
        Assert.Throws<ArgumentException>(() =>
            MediaStorageKeys.Variant(Ulid.NewUlid(), MediaVariantType.Original));
    }
}
