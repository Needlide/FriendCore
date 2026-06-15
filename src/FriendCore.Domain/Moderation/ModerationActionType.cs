namespace FriendCore.Domain.Moderation;

// §13.2 enforcement ladder, plus the no-enforcement outcome (ReportDismissed). MVP runtime
// effect: ContentRemoval (soft-delete), TemporarySuspension (SuspendedUntil), PermanentBan
// (IsBanned). Warning / TemporaryRestriction / LegalReferral / ReportDismissed are recorded in
// the audit log only — no distinct runtime mechanism yet.
public enum ModerationActionType
{
    ContentRemoval,
    Warning,
    TemporaryRestriction,
    TemporarySuspension,
    PermanentBan,
    LegalReferral,
    ReportDismissed
}
