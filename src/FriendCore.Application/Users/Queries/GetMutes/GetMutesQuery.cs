using ErrorOr;
using MediatR;
using FriendCore.Application.Common.Models;

namespace FriendCore.Application.Users.Queries.GetMutes;

public sealed record GetMutesQuery : IRequest<ErrorOr<MuteListResponse>>;
