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
    public string Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public Ulid? AvatarMediaId { get; private set; }
    public UserProfile Profile { get; private set; } = null!;
    public ContentPreference ContentPreference { get; private set; }
    public UserRole Role { get; private set; }
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
            Role = UserRole.User,
            UpdatedAt = createdAt,
            LastActiveAt = createdAt
        };

        user.RaiseDomainEvent(new UserRegisteredEvent(
            user.Id,
            user.Email,
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

    public void AssignRole(UserRole role)
    {
        Role = role;
    }

    public static string ToUsernameSlug(string username)
    {
        return username.Trim().ToLowerInvariant();
    }
}