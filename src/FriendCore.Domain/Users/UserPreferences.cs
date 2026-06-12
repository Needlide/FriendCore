using ErrorOr;

namespace FriendCore.Domain.Users;

public sealed class UserPreferences
{
    public const int MaxKeywordFilters = 50;
    public const int MaxKeywordFilterLength = 100;

    private List<string> _keywordFilters = [];

    private UserPreferences()
    {
    }

    public Ulid UserId { get; private set; }
    public bool HideFlashingMedia { get; private set; } = true;
    public bool ReduceMotion { get; private set; }
    public bool DisableAutoplay { get; private set; } = true;
    public bool ReduceVisualNoise { get; private set; }
    public ContentPreference ContentPreference { get; private set; } = ContentPreference.General;
    public IReadOnlyList<string> KeywordFilters => _keywordFilters;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; set; }

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