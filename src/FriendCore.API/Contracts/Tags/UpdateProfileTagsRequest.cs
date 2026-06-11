using FriendCore.Application.Tags.Commands.UpdateProfileTags;

namespace FriendCore.API.Contracts.Tags;

public sealed record UpdateProfileTagsRequest(IReadOnlyList<ProfileTagItemRequest> Tags);

public sealed record ProfileTagItemRequest(string Name, ProfileTagTier Tier, bool IsPublic);
