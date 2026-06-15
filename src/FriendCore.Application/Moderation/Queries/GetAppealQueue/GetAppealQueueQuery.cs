using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Moderation.Queries.GetAppealQueue;

public sealed record GetAppealQueueQuery : IRequest<ErrorOr<AppealQueueResponse>>;
