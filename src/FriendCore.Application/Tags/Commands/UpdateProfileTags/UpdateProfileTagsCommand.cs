using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Tags.Commands.UpdateProfileTags;

public sealed record UpdateProfileTagsCommand(IReadOnlyList<ProfileTagInput> Tags)
    : IRequest<ErrorOr<ProfileTagsResponse>>;

public sealed record ProfileTagInput(string Name, ProfileTagTier Tier, bool IsPublic);

// Application-level mirror of Domain TagTier so API contracts never bind Domain types
// (§2.2 layering). Member names are the wire format (JsonStringEnumConverter) — renaming
// here is a breaking API change; renaming the Domain enum is not.
public enum ProfileTagTier
{
    Primary,
    Secondary
}
