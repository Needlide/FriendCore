using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Tags.Commands.CreateOrGetTag;

public sealed record CreateOrGetTagCommand(string Name) : IRequest<ErrorOr<TagResponse>>;