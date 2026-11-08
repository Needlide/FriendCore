namespace FriendCore.Application.Common.Models;

public sealed record UserSearchResponse(IReadOnlyList<UserSummaryResponse> Users);
