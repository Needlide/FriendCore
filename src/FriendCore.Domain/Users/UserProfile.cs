namespace FriendCore.Domain.Users;

public sealed class UserProfile
{
    private UserProfile()
    {
    }

    public string? Bio { get; private set; }
    public string? WebsiteUrl { get; private set; }
    public string? Location { get; private set; }
    public DateTimeOffset? UsernameLastChangedAt { get; private set; }

    public static UserProfile Empty()
    {
        return new UserProfile();
    }
}