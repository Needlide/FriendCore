namespace FriendCore.Domain.Users;

public sealed class UserPreferences
{
    private UserPreferences()
    {
    }

    public Ulid UserId { get; private set; }
    public bool HideFlashingMedia { get; private set; } = true;
    public bool ReduceMotion { get; private set; }
    public bool DisableAutoplay { get; private set; } = true;
    public bool ReduceVisualNoise { get; private set; }
    public ContentPreference ContentPreference { get; private set; } = ContentPreference.General;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; set; }

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