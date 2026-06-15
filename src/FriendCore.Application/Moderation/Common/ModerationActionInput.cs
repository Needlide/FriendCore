using FriendCore.Domain.Moderation;

namespace FriendCore.Application.Moderation.Common;

// Application-level mirror of Domain ModerationActionType (§2.2 layering). "Dismiss" maps to the
// domain's ReportDismissed; the other members are 1:1.
public enum ModerationActionInput
{
    ContentRemoval,
    Warning,
    TemporaryRestriction,
    TemporarySuspension,
    PermanentBan,
    LegalReferral,
    Dismiss
}

internal static class ModerationActionMapping
{
    public static ModerationActionType ToDomain(this ModerationActionInput input) =>
        input switch
        {
            ModerationActionInput.ContentRemoval => ModerationActionType.ContentRemoval,
            ModerationActionInput.Warning => ModerationActionType.Warning,
            ModerationActionInput.TemporaryRestriction => ModerationActionType.TemporaryRestriction,
            ModerationActionInput.TemporarySuspension => ModerationActionType.TemporarySuspension,
            ModerationActionInput.PermanentBan => ModerationActionType.PermanentBan,
            ModerationActionInput.LegalReferral => ModerationActionType.LegalReferral,
            ModerationActionInput.Dismiss => ModerationActionType.ReportDismissed,
            _ => throw new ArgumentOutOfRangeException(nameof(input), input, null)
        };
}
