using FriendCore.Application.Moderation.Common;

namespace FriendCore.API.Contracts.Moderation;

public sealed record ActOnUserRequest(
    ModerationActionInput Action,
    string Reason,
    int? SuspensionDays = null);
