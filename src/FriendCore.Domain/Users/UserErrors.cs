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

    public static Error AccountSuspended =>
        Error.Forbidden("User.AccountSuspended", "This account is temporarily suspended.");

    public static Error InvalidUsername =>
        Error.Validation("User.InvalidUsername",
            "Username must be 3–30 characters and contain only letters, numbers, or underscores.");

    public static Error InvalidPassword =>
        Error.Validation("User.Password.TooShort", "Password must be at least 8 characters.");

    public static Error InvalidEmail =>
        Error.Validation("User.InvalidEmail", "A valid email address is required.");

    public static Error NotFound =>
        Error.NotFound("User.NotFound", "User was not found.");

    public static Error CannotFollowSelf =>
        Error.Validation("User.CannotFollowSelf", "You cannot follow yourself.");

    public static Error CannotBlockSelf =>
        Error.Validation("User.CannotBlockSelf", "You cannot block yourself.");

    // The admin account staffs the support inbox — blocking it would sever the user's help
    // channel, so it is rejected (400) rather than hidden. Admins moderate via suspension/ban,
    // never via blocks.
    public static Error CannotBlockAdmin =>
        Error.Validation("User.CannotBlockAdmin", "This account cannot be blocked.");

    public static Error InvalidSnoozeWindow =>
        Error.Validation("User.InvalidSnoozeWindow", "Snooze must end in the future and within 24 hours.");

    public static Error CannotMuteSelf =>
        Error.Validation("User.CannotMuteSelf", "You cannot mute yourself.");

    public static Error TooManyKeywordFilters =>
        Error.Validation("User.TooManyKeywordFilters",
            $"You can have at most {UserPreferences.MaxKeywordFilters} keyword filters.");

    public static Error KeywordFilterTooLong =>
        Error.Validation("User.KeywordFilterTooLong",
            $"Each keyword filter must be at most {UserPreferences.MaxKeywordFilterLength} characters.");

    public static Error BlockRequiresConfirmation =>
        Error.Conflict("User.BlockRequiresConfirmation",
            "A follow relationship exists with this user. Blocking will remove it; re-send with confirmed=true to proceed.");

    public static Error EmailAlreadyVerified =>
        Error.Conflict("User.EmailAlreadyVerified", "Email is already verified.");

    public static Error EmailNotVerified =>
        Error.Validation("User.EmailNotVerified", "Please verify your email before continuing.");

    public static Error InvalidVerificationToken =>
        Error.Validation("User.InvalidVerificationToken", "Verification link is invalid or has expired.");

    public static Error InvalidResetToken =>
        Error.Validation("User.InvalidResetToken", "Reset link is invalid or has expired.");

    public static Error InvalidRefreshToken =>
        Error.Unauthorized("User.InvalidRefreshToken", "The session is invalid or has expired. Please sign in again.");
}