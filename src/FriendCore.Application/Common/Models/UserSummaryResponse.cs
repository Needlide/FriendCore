using FriendCore.Domain.Users;

namespace FriendCore.Application.Common.Models;

public sealed record UserSummaryResponse(
    string UserId,
    string Username,
    string DisplayName,
    bool IsVerified)
{
    public static UserSummaryResponse FromUser(User user) =>
        new(user.Id.ToString(), user.Username, user.DisplayName, user.IsVerified);
}

public sealed record FollowListResponse(IReadOnlyList<UserSummaryResponse> Users);
