using ErrorOr;

namespace FriendCore.Domain.Users;

public sealed class UserPreferences
{
    public const int MaxKeywordFilters = 50;
    public const int MaxKeywordFilterLength = 100;

    private List<string> _keywordFilters = [];

    // NotificationType names the user muted (empty = everything notifies).
    // Stored as plain strings (text[] like KeywordFilters) so unknown future types
    // fail open rather than crash reads.
    private List<string> _mutedNotificationTypes = [];

    private UserPreferences()
    {
    }

    public Ulid UserId { get; private set; }
    public bool HideFlashingMedia { get; private set; } = true;
    public bool ReduceMotion { get; private set; }
    public bool DisableAutoplay { get; private set; } = true;
    public bool ReduceVisualNoise { get; private set; }
    public IReadOnlyList<string> KeywordFilters => _keywordFilters;
    public IReadOnlyList<string> MutedNotificationTypes => _mutedNotificationTypes;

    /// <summary>Silence-all until this instant (UTC); null means off. See notification snooze.</summary>
    public DateTimeOffset? SnoozedUntilUtc { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public void UpdateAccessibility(
        bool hideFlashingMedia,
        bool reduceMotion,
        bool disableAutoplay,
        bool reduceVisualNoise,
        DateTimeOffset now)
    {
        HideFlashingMedia = hideFlashingMedia;
        ReduceMotion = reduceMotion;
        DisableAutoplay = disableAutoplay;
        ReduceVisualNoise = reduceVisualNoise;
        UpdatedAt = now;
    }

    /// <summary>
    ///     Whole-set replace (§13.6 keyword filter). Normalisation makes matching case-insensitive
    ///     by construction: lowercase + trim, empties and duplicates dropped. Backslashes are
    ///     stripped because they are the ILIKE escape character; '%' and '_' are left alone — as
    ///     wildcards they can only hide MORE, which is the safe direction for a self-protection
    ///     feature.
    /// </summary>
    public ErrorOr<Success> SetKeywordFilters(IReadOnlyList<string> keywords, DateTimeOffset now)
    {
        var normalized = keywords
            .Select(keyword => keyword.Trim().ToLowerInvariant().Replace("\\", string.Empty))
            .Where(keyword => keyword.Length > 0)
            .Distinct()
            .ToList();

        if (normalized.Count > MaxKeywordFilters)
            return UserErrors.TooManyKeywordFilters;

        if (normalized.Any(keyword => keyword.Length > MaxKeywordFilterLength))
            return UserErrors.KeywordFilterTooLong;

        _keywordFilters = normalized;
        UpdatedAt = now;
        return Result.Success;
    }

    /// <summary>
    ///     Whole-set replace of muted notification types. Unknown names are dropped (never
    ///     stored) so a client sending a future type degrades to "not muted", not an error.
    /// </summary>
    public void SetMutedNotificationTypes(IReadOnlyList<string> types, DateTimeOffset now)
    {
        _mutedNotificationTypes = types
            .Where(t => Enum.TryParse<Notifications.NotificationType>(t, out _))
            .Distinct()
            .ToList();
        UpdatedAt = now;
    }

    public bool Mutes(Notifications.NotificationType type)
    {
        return _mutedNotificationTypes.Contains(type.ToString());
    }

    public void SetSnoozedUntil(DateTimeOffset? untilUtc, DateTimeOffset now)
    {
        SnoozedUntilUtc = untilUtc;
        UpdatedAt = now;
    }

    /// <summary>
    ///     Single silencing predicate (mute and snooze compose with OR). Written as the one
    ///     place a future quiet-hours schedule extends — a second clause, not a redesign.
    /// </summary>
    public bool IsSilenced(Notifications.NotificationType type, DateTimeOffset now)
    {
        return Mutes(type) || (SnoozedUntilUtc is { } until && until > now);
    }

    public static UserPreferences CreateDefault(Ulid userId, DateTimeOffset createdAt)
    {
        return new UserPreferences
        {
            UserId = userId,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };
    }
}