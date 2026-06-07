using ErrorOr;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Tags;
using MediatR;

namespace FriendCore.Application.Tags.Commands.UpdateProfileTags;

public sealed record UpdateProfileTagsCommand(IReadOnlyList<ProfileTagInput> Tags)
    : IRequest<ErrorOr<ProfileTagsResponse>>;

public sealed record ProfileTagInput(string Name, TagTier Tier, bool IsPublic);