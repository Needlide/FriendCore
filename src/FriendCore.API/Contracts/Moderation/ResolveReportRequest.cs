using FriendCore.Application.Moderation.Common;

namespace FriendCore.API.Contracts.Moderation;

public sealed record ResolveReportRequest(
    ModerationActionInput Action,
    string Reason,
    int? SuspensionDays = null);
