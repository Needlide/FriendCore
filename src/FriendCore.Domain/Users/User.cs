using ErrorOr;
using FriendCore.Domain.Common;
using FriendCore.Domain.Tags;
using FriendCore.Domain.Users.Events;

namespace FriendCore.Domain.Users;

public sealed class User : AggregateRoot<Ulid>, ISoftDeletable, IAuditable
{
    private readonly List<ProfileTag> _profileTags = [];

    private User(Ulid id, DateTimeOffset createdAt)
        : base(id, createdAt)
    {
    }

    private User()
    {
    }

    public IReadOnlyList<ProfileTag> ProfileTags => _profileTags.AsReadOnly();

    public string Username { get; private set; } = null!;
    public string UsernameSlug { get; private set; } = null!;
    public string DisplayName { get; private set; } = null!;
    /// <summary>
    ///     HMAC-SHA256 of the normalized email (lowercase hex). The plaintext address is NEVER
    ///     persisted — it exists transiently per request (verification and reset sends) only.
    /// </summary>
    public string EmailHash { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public Ulid? AvatarMediaId { get; private set; }
    public UserProfile Profile { get; private set; } = null!;
    public ContentPreference ContentPreference { get; private set; }
    public UserRole Role { get; private set; }
    public bool IsVerified { get; private set; }
    public bool IsBanned { get; private set; }
    public DateTimeOffset? SuspendedUntil { get; private set; }
    public DateTimeOffset? LastActiveAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public static ErrorOr<User> Register(
        string username,
        string displayName,
        string emailHash,
        string contactEmail,
        string passwordHash,
        DateTimeOffset createdAt)
    {
        var normalizedUsername = username.Trim();
        var slug = ToUsernameSlug(normalizedUsername);

        if (string.IsNullOrWhiteSpace(slug) || slug.Length < 3 || slug.Length > 30)
            return UserErrors.InvalidUsername;

        if (string.IsNullOrWhiteSpace(displayName))
            return Error.Validation("User.InvalidDisplayName", "Display name is required.");

        // Only the hash reaches the domain — plaintext format is validated at the edge
        // (command validators) before hashing.
        if (string.IsNullOrWhiteSpace(emailHash))
            return UserErrors.InvalidEmail;

        var user = new User(Ulid.NewUlid(), createdAt)
        {
            Username = normalizedUsername,
            UsernameSlug = slug,
            DisplayName = displayName.Trim(),
            EmailHash = emailHash,
            PasswordHash = passwordHash,
            Profile = UserProfile.Empty(),
            ContentPreference = ContentPreference.General,
            Role = UserRole.User,
            UpdatedAt = createdAt,
            LastActiveAt = createdAt
        };

        user.RaiseDomainEvent(new UserRegisteredEvent(
            user.Id,
            contactEmail,
            user.DisplayName,
            user.Username,
            createdAt));
        return user;
    }

    public ErrorOr<Success> UpdateProfile(
        string? displayName,
        string? bio,
        string? websiteUrl,
        string? location,
        DateTimeOffset updatedAt)
    {
        if (displayName is not null)
        {
            var trimmed = displayName.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.Length > 100)
                return Error.Validation("User.InvalidDisplayName", "Display name must be 1–100 characters.");

            DisplayName = trimmed;
        }

        Profile.Update(bio, websiteUrl, location);
        UpdatedAt = updatedAt;
        return Result.Success;
    }

    public void SetContentPreference(ContentPreference preference, DateTimeOffset at)
    {
        ContentPreference = preference;
        UpdatedAt = at;
    }

    public ErrorOr<Success> AddProfileTag(Ulid tagId, TagTier tier, bool isPublic, DateTimeOffset at)
    {
        if (_profileTags.Any(tag => tag.TagId == tagId))
            return TagErrors.Duplicate;

        var tierCount = _profileTags.Count(tag => tag.Tier == tier);
        switch (tier)
        {
            case TagTier.Primary when tierCount >= TagLimits.MaxPrimaryProfileTags:
                return TagErrors.TooManyPrimaryTags;
            case TagTier.Secondary when tierCount >= TagLimits.MaxSecondaryProfileTags:
                return TagErrors.TooManySecondaryTags;
        }

        _profileTags.Add(ProfileTag.Create(Id, tagId, tier, isPublic, at));
        UpdatedAt = at;
        return Result.Success;
    }

    public bool RemoveProfileTag(Ulid tagId)
    {
        var existing = _profileTags.FirstOrDefault(tag => tag.TagId == tagId);
        if (existing is null)
            return false;

        _profileTags.Remove(existing);
        return true;
    }

    public bool UpdateProfileTag(Ulid tagId, TagTier tier, bool isPublic, DateTimeOffset at)
    {
        var existing = _profileTags.FirstOrDefault(tag => tag.TagId == tagId);
        if (existing is null)
            return false;

        existing.Update(tier, isPublic);
        UpdatedAt = at;
        return true;
    }

    public ErrorOr<Success> MarkEmailVerified(DateTimeOffset verifiedAt)
    {
        if (IsVerified)
            return UserErrors.EmailAlreadyVerified;

        IsVerified = true;
        UpdatedAt = verifiedAt;
        return Result.Success;
    }

    public void RecordLogin(DateTimeOffset at)
    {
        LastActiveAt = at;
    }

    public void ChangePassword(string passwordHash, DateTimeOffset at)
    {
        PasswordHash = passwordHash;
        UpdatedAt = at;
    }

    public void AssignRole(UserRole role)
    {
        Role = role;
    }

    // §13.2 enforcement. Suspension blocks login until the given time; ban is permanent. Login
    // is the enforcement point (LoginUser checks both) — existing short-lived tokens are not
    // revoked mid-session in the MVP.
    public bool IsSuspended(DateTimeOffset now) => SuspendedUntil is { } until && until > now;

    public void Suspend(DateTimeOffset until, DateTimeOffset at)
    {
        SuspendedUntil = until;
        UpdatedAt = at;
    }

    public void Ban(DateTimeOffset at)
    {
        IsBanned = true;
        UpdatedAt = at;
    }

    // Reverses ban and suspension — used when an appeal is overturned (§13.3).
    public void Reinstate(DateTimeOffset at)
    {
        IsBanned = false;
        SuspendedUntil = null;
        UpdatedAt = at;
    }

    public static string ToUsernameSlug(string username)
    {
        return username.Trim().ToLowerInvariant();
    }

    /// <summary>Canonical form hashed for storage and lookup (never the raw address). </summary>
    public static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }
}