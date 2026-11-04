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

    /// <summary>
    ///     Lowercase hex SHA-256 of the uploaded bytes as received. Null only for rows created before
    ///     this column existed, which means "unknown" and must never be treated as a match.
    /// </summary>
    /// <remarks>
    ///     Computed over the ORIGINAL — never a variant — and never accepted from the client, since
    ///     a client-supplied hash is trivially forged and would defeat every use this column exists
    ///     for. Nothing consumes it yet: it is the prerequisite for hash matching (CSAM), for
    ///     consent-withdrawal on a deleted image, and for processed-variant reuse. All three need
    ///     the column to exist before they are worth building, and it is far cheaper to add now
    ///     than to retrofit once there are rows to backfill.
    /// </remarks>
    public string? ContentHash { get; private set; }

    public MediaStatus Status { get; private set; }

    /// <summary>
    ///     Creator-declared and moderator-applied safety flags, stored as one JSONB dictionary.
    ///     Keys are flag names ("flashing", "arachnophobia", ...), values are booleans. Additive
    ///     — flags are only ever set to true, never cleared.
    /// </summary>
    public MediaSafetyFlags SafetyFlags { get; private set; } = MediaSafetyFlags.Empty;

    /// <summary>
    ///     Who last set the safety flags: user self-declaration, a user report, or a moderator.
    ///     A moderator's decision outranks a later report.
    /// </summary>
    public MediaSafetySource SafetySource { get; private set; }
    public DateTimeOffset? SafetyFlaggedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public IReadOnlyList<MediaVariant> Variants => _variants.AsReadOnly();

    public bool IsReady => Status == MediaStatus.Ready;

    // Status starts as Processing; variants are populated later by ProcessMediaJob. A safety flag
    // declared at upload time is recorded as a UserSelfMarked safety source.
    public static MediaItem Create(
        Ulid uploaderId,
        string originalFileName,
        string contentType,
        string fileExtension,
        MediaSafetyFlags safetyFlags,
        DateTimeOffset createdAt,
        string? contentHash = null)
    {
        var selfDeclared = safetyFlags.Flags.Values.Any(value => value);
        var id = Ulid.NewUlid();

        return new MediaItem(id, createdAt)
        {
            UploaderId = uploaderId,
            OriginalFileName = originalFileName,
            ContentType = contentType,
            OriginalStorageKey = MediaStorageKeys.Original(uploaderId, id, fileExtension),
            Status = MediaStatus.Processing,
            SafetyFlags = safetyFlags,
            SafetySource = selfDeclared ? MediaSafetySource.UserSelfMarked : MediaSafetySource.None,
            SafetyFlaggedAt = selfDeclared ? createdAt : null,
            ContentHash = contentHash
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

    // Records additional safety warnings from a user report or moderator action. Flags are
    // additive – never cleared here. A moderator decision is authoritative and is not overwritten
    // by a later report.
    public void FlagSafety(MediaSafetyFlags additionalFlags, MediaSafetySource source, DateTimeOffset at)
    {
        SafetyFlags = SafetyFlags.Merge(additionalFlags);

        if (SafetySource != MediaSafetySource.Moderator)
            SafetySource = source;

        SafetyFlaggedAt = at;
    }
}
