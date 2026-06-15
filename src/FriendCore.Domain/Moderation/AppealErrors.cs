using ErrorOr;

namespace FriendCore.Domain.Moderation;

public static class AppealErrors
{
    public static Error NotFound =>
        Error.NotFound("Appeal.NotFound", "Appeal was not found.");

    public static Error ActionNotFound =>
        Error.NotFound("Appeal.ActionNotFound", "The moderation action was not found.");

    public static Error NotTheAffectedUser =>
        Error.NotFound("Appeal.NotTheAffectedUser", "The moderation action was not found.");

    public static Error NotAppealable =>
        Error.Validation("Appeal.NotAppealable", "This moderation action cannot be appealed.");

    public static Error AlreadyAppealed =>
        Error.Conflict("Appeal.AlreadyAppealed", "You have already appealed this action.");

    public static Error ReasonRequired =>
        Error.Validation("Appeal.ReasonRequired", "An appeal reason is required.");

    public static Error ReasonTooLong =>
        Error.Validation("Appeal.ReasonTooLong", $"Appeal reason must not exceed {Appeal.MaxReasonLength} characters.");

    public static Error ReviewNoteRequired =>
        Error.Validation("Appeal.ReviewNoteRequired", "A review note is required.");

    public static Error AlreadyReviewed =>
        Error.Conflict("Appeal.AlreadyReviewed", "This appeal has already been reviewed.");

    // §13.3: appeals are reviewed by a different moderator than the one who took the action.
    public static Error SameModerator =>
        Error.Forbidden("Appeal.SameModerator",
            "An appeal must be reviewed by a different moderator than the one who took the action.");
}
