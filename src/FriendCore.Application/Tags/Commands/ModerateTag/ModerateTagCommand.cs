using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Tags.Commands.ModerateTag;

public sealed record ModerateTagCommand(Ulid TagId, TagModerationAction Action) : IRequest<ErrorOr<TagResponse>>;

public enum TagModerationAction
{
    Approve,
    Archive,
    Ban
}
