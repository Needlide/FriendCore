namespace FriendCore.Domain.Moderation;

// §13.2 enforcement ladder, plus the no-enforcement outcome (ReportDismissed) and the admin
// RBAC action (RoleChanged). MVP runtime effect: ContentRemoval (soft-delete),
// TemporarySuspension (SuspendedUntil), PermanentBan (IsBanned). Warning / TemporaryRestriction /
// LegalReferral / ReportDismissed are recorded in the audit log only — no distinct runtime
// mechanism. RoleChanged records an admin role assignment.
public enum ModerationActionType
{
    ContentRemoval,
    Warning,
    TemporaryRestriction,
    TemporarySuspension,
    PermanentBan,
    LegalReferral,
    ReportDismissed,
    RoleChanged
}
