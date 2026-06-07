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

    internal void Update(string? bio, string? websiteUrl, string? location)
    {
        if (bio != null)
            Bio = NormalizeOptional(bio);
        
        if (websiteUrl != null)
            WebsiteUrl = NormalizeOptional(websiteUrl);
        
        if (location != null)
            Location = NormalizeOptional(location);
    }

    private static string? NormalizeOptional(string? value)
    {
        if (value is null)
            return null;

        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}