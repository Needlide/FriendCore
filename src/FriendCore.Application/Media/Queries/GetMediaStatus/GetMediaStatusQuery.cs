using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Media.Queries.GetMediaStatus;

public sealed record GetMediaStatusQuery(Ulid MediaId) : IRequest<ErrorOr<MediaStatusResponse>>;
