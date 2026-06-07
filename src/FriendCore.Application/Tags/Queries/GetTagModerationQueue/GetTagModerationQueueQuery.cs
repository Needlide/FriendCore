using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Tags.Queries.GetTagModerationQueue;

public sealed record GetTagModerationQueueQuery : IRequest<ErrorOr<TagModerationQueueResponse>>;
