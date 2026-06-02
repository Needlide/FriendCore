using ErrorOr;

namespace FriendCore.Domain.Users;

public static class UserErrors
{
    public static Error EmailAlreadyInUse =>
        Error.Conflict("User.EmailAlreadyInUse", "An account with this email already exists.");

    public static Error UsernameAlreadyInUse =>
        Error.Conflict("User.UsernameAlreadyInUse", "This username is already taken.");

    public static Error InvalidCredentials =>
        Error.Unauthorized("User.InvalidCredentials", "Email or password is incorrect.");

    public static Error AccountBanned =>
        Error.Forbidden("User.AccountBanned", "This account has been suspended.");

    public static Error InvalidUsername =>
        Error.Validation("User.InvalidUsername",
            "Username must be 3–30 characters and contain only letters, numbers, or underscores.");

    public static Error InvalidPassword =>
        Error.Validation("User.Password.TooShort", "Password must be at least 8 characters.");

    public static Error InvalidEmail =>
        Error.Validation("User.InvalidEmail", "A valid email address is required.");
}