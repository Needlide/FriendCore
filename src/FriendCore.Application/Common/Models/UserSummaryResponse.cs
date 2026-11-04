using FriendCore.Domain.Users;

namespace FriendCore.Application.Common.Models;

public sealed record UserSummaryResponse(
    string UserId,
    string Username,
    string DisplayName,
    bool IsVerified,
    bool IsFollowedByViewer = false)
{
    public static UserSummaryResponse FromUser(User user, bool isFollowedByViewer = false) =>
        new(user.Id.ToString(), user.Username, user.DisplayName, user.IsVerified, isFollowedByViewer);
}

public sealed record FollowListResponse(IReadOnlyList<UserSummaryResponse> Users);
