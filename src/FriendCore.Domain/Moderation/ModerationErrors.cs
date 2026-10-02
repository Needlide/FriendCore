using ErrorOr;

namespace FriendCore.Domain.Moderation;

public static class ModerationErrors
{
    // Removal acts on content, never on a person. Post and Message are the content targets;
    // a User target has no content to remove, so the ladder escalates to a warning or ban.
    public static Error ContentRemovalRequiresContent =>
        Error.Validation("Moderation.ContentRemovalRequiresContent",
            "Content removal can only be applied to a post or message report.");

    public static Error ActionRequiresUser =>
        Error.Validation("Moderation.ActionRequiresUser",
            "This action applies to a user, not to content directly.");

    public static Error InvalidSuspensionDuration =>
        Error.Validation("Moderation.InvalidSuspensionDuration",
            "A suspension requires a positive number of days.");

    public static Error ReasonRequired =>
        Error.Validation("Moderation.ReasonRequired", "A reason is required for every moderation action.");

    public static Error TargetNotFound =>
        Error.NotFound("Moderation.TargetNotFound", "The action target was not found.");

    public static Error CannotChangeOwnRole =>
        Error.Validation("Moderation.CannotChangeOwnRole", "You cannot change your own role.");
}
