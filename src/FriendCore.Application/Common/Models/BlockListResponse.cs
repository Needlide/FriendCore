namespace FriendCore.Application.Common.Models;

public sealed record BlockListResponse(IReadOnlyList<UserSummaryResponse> Users);
