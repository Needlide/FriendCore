namespace FriendCore.Application.Common.Models;

public sealed record MuteListResponse(IReadOnlyList<UserSummaryResponse> Users);
