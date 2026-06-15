using ErrorOr;

namespace FriendCore.Domain.Moderation;

public static class ModerationErrors
{
    public static Error ContentRemovalRequiresPost =>
        Error.Validation("Moderation.ContentRemovalRequiresPost",
            "Content removal can only be applied to a post report.");

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
}
