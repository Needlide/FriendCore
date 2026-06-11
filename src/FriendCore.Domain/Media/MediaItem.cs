using FriendCore.Domain.Common;

namespace FriendCore.Domain.Media;

public sealed class MediaItem : AggregateRoot<Ulid>, ISoftDeletable
{
    private readonly List<MediaVariant> _variants = [];

    private MediaItem(Ulid id, DateTimeOffset createdAt)
        : base(id, createdAt)
    {
    }

    private MediaItem()
    {
    }

    public Ulid UploaderId { get; private set; }
    public string OriginalFileName { get; private set; } = null!;
    public string ContentType { get; private set; } = null!;

    // Bucket-relative key of the preserved original (spec §7.1/§7.3). Stored as a key, not a URL —
    // originals are only ever served via short-lived presigned URLs.
    public string OriginalStorageKey { get; private set; } = null!;

    public MediaStatus Status { get; private set; }
    public bool HasFlashingWarning { get; private set; }
    public bool HasIntenseMotionWarning { get; private set; }
    public MediaSafetySource SafetySource { get; private set; }
    public DateTimeOffset? SafetyFlaggedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public IReadOnlyList<MediaVariant> Variants => _variants.AsReadOnly();

    public bool IsReady => Status == MediaStatus.Ready;

    // Status starts as Processing; variants are populated later by ProcessMediaJob. A flashing or
    // intense-motion warning declared at upload time is recorded as a UserSelfMarked safety source.
    public static MediaItem Create(
        Ulid uploaderId,
        string originalFileName,
        string contentType,
        string fileExtension,
        bool hasFlashingWarning,
        bool hasIntenseMotionWarning,
        DateTimeOffset createdAt)
    {
        var selfDeclared = hasFlashingWarning || hasIntenseMotionWarning;
        var id = Ulid.NewUlid();

        return new MediaItem(id, createdAt)
        {
            UploaderId = uploaderId,
            OriginalFileName = originalFileName,
            ContentType = contentType,
            OriginalStorageKey = MediaStorageKeys.Original(uploaderId, id, fileExtension),
            Status = MediaStatus.Processing,
            HasFlashingWarning = hasFlashingWarning,
            HasIntenseMotionWarning = hasIntenseMotionWarning,
            SafetySource = selfDeclared ? MediaSafetySource.UserSelfMarked : MediaSafetySource.None,
            SafetyFlaggedAt = selfDeclared ? createdAt : null
        };
    }

    // Called by ProcessMediaJob once variants are generated. No-op if already finalised, so a
    // duplicate or replayed job cannot move a Ready/Failed item back into a half-processed state.
    public void MarkReady(IEnumerable<MediaVariant> variants)
    {
        if (Status != MediaStatus.Processing)
            return;

        _variants.Clear();
        _variants.AddRange(variants);
        Status = MediaStatus.Ready;
    }

    // Called by ProcessMediaJob when variant generation fails. No-op if already finalised.
    public void MarkFailed()
    {
        if (Status != MediaStatus.Processing)
            return;

        Status = MediaStatus.Failed;
    }

    // Records a safety warning from a user report or moderator action. Flags are additive – never
    // cleared here. A moderator decision is authoritative and is not overwritten by a later report.
    public void FlagSafety(bool flashing, bool intenseMotion, MediaSafetySource source, DateTimeOffset at)
    {
        if (flashing)
            HasFlashingWarning = true;

        if (intenseMotion)
            HasIntenseMotionWarning = true;

        if (SafetySource != MediaSafetySource.Moderator)
            SafetySource = source;

        SafetyFlaggedAt = at;
    }
}
