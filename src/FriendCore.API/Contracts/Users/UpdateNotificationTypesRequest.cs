namespace FriendCore.API.Contracts.Users;

/// <summary>Whole-set replace: the submitted type names become the complete muted set.</summary>
public sealed record UpdateNotificationTypesRequest(IReadOnlyList<string> MutedTypes);
