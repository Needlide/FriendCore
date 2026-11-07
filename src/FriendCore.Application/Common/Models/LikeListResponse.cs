namespace FriendCore.Application.Common.Models;

/// <summary>
///     Who liked a post, newest first. Users hidden by a block relationship are omitted by the
///     repository's user filter; the post's denormalised LikeCount may therefore be higher than
///     the number of rows returned (the same accepted trade-off as other block-filtered lists).
/// </summary>
public sealed record LikeListResponse(IReadOnlyList<UserSummaryResponse> Users);
