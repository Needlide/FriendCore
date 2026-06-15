using FriendCore.Domain.Users;

namespace FriendCore.Application.Moderation.Common;

// Application-level mirror of Domain UserRole so API contracts never bind Domain types (§2.2).
public enum UserRoleInput
{
    User,
    Moderator,
    SeniorModerator,
    Admin
}

internal static class UserRoleMapping
{
    public static UserRole ToDomain(this UserRoleInput input) =>
        input switch
        {
            UserRoleInput.User => UserRole.User,
            UserRoleInput.Moderator => UserRole.Moderator,
            UserRoleInput.SeniorModerator => UserRole.SeniorModerator,
            UserRoleInput.Admin => UserRole.Admin,
            _ => throw new ArgumentOutOfRangeException(nameof(input), input, null)
        };
}
