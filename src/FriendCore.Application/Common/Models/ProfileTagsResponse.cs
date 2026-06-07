namespace FriendCore.Application.Common.Models;

public sealed record ProfileTagsResponse(IReadOnlyList<ProfileTagItem> Tags);

public sealed record ProfileTagItem(
    string TagId,
    string Name,
    string Slug,
    string Tier,
    bool IsPublic);