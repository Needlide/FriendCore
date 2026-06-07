using FriendCore.Domain.Tags;

namespace FriendCore.API.Contracts.Tags;

public sealed record UpdateProfileTagsRequest(IReadOnlyList<ProfileTagItemRequest> Tags);

public sealed record ProfileTagItemRequest(string Name, TagTier Tier, bool IsPublic);