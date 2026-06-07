using FriendCore.Domain.Users;

namespace FriendCore.UnitTests.Users;

internal static class UserTestFactory
{
    public static readonly DateTimeOffset DefaultCreatedAt = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    public static User Create(
        string username = "alice",
        string displayName = "Alice",
        string email = "alice@example.com",
        DateTimeOffset? createdAt = null)
    {
        var result = User.Register(username, displayName, email, "hashed-password", createdAt ?? DefaultCreatedAt);
        return result.Value;
    }

    public static User CreateVerified(DateTimeOffset? verifiedAt = null)
    {
        var user = Create();
        user.MarkEmailVerified(verifiedAt ?? DefaultCreatedAt);
        return user;
    }

    // No domain ban API exists yet; the IsBanned setter is private. Set it via
    // reflection so the AccountBanned guard stays covered. Replace with a real
    // User.Ban(...) call once the moderation phase adds one.
    // TODO replace with a real Ban logic
    public static User CreateBanned()
    {
        var user = Create();
        typeof(User).GetProperty(nameof(User.IsBanned))!.SetValue(user, true);
        return user;
    }
}