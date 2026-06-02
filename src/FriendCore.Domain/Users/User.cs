using ErrorOr;
using FriendCore.Domain.Common;
using FriendCore.Domain.Users.Events;

namespace FriendCore.Domain.Users;

public sealed class User : AggregateRoot<Ulid>, ISoftDeletable, IAuditable
{
    private User(Ulid id, DateTimeOffset createdAt)
        : base(id, createdAt)
    {
    }

    private User()
    {
    }

    public string Username { get; private set; } = null!;
    public string UsernameSlug { get; private set; } = null!;
    public string DisplayName { get; private set; } = null!;
    public string Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public Ulid? AvatarMediaId { get; private set; }
    public UserProfile Profile { get; private set; } = null!;
    public ContentPreference ContentPreference { get; private set; }
    public bool IsVerified { get; private set; }
    public bool IsBanned { get; private set; }
    public DateTimeOffset? LastActiveAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public static ErrorOr<User> Register(
        string username,
        string displayName,
        string email,
        string passwordHash,
        DateTimeOffset createdAt)
    {
        var normalizedUsername = username.Trim();
        var slug = ToUsernameSlug(normalizedUsername);

        if (string.IsNullOrWhiteSpace(slug) || slug.Length < 3 || slug.Length > 30)
            return UserErrors.InvalidUsername;

        if (string.IsNullOrWhiteSpace(displayName))
            return Error.Validation("User.InvalidDisplayName", "Display name is required.");

        var normalizedEmail = email.Trim().ToLowerInvariant();
        if (normalizedEmail.Length is < 3 or > 320 || !normalizedEmail.Contains('@'))
            return UserErrors.InvalidEmail;

        var user = new User(Ulid.NewUlid(), createdAt)
        {
            Username = normalizedUsername,
            UsernameSlug = slug,
            DisplayName = displayName.Trim(),
            Email = normalizedEmail,
            PasswordHash = passwordHash,
            Profile = UserProfile.Empty(),
            ContentPreference = ContentPreference.General,
            UpdatedAt = createdAt,
            LastActiveAt = createdAt
        };

        user.RaiseDomainEvent(new UserRegisteredEvent(user.Id, user.Email, createdAt));
        return user;
    }

    public void RecordLogin(DateTimeOffset at)
    {
        LastActiveAt = at;
    }

    public static string ToUsernameSlug(string username)
    {
        return username.Trim().ToLowerInvariant();
    }
}