namespace FriendCore.API.Contracts.Moderation;

public sealed record SubmitAppealRequest(string AuditLogId, string Reason);
