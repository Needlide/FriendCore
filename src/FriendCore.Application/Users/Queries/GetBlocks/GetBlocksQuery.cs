using ErrorOr;
using MediatR;
using FriendCore.Application.Common.Models;

namespace FriendCore.Application.Users.Queries.GetBlocks;

public sealed record GetBlocksQuery : IRequest<ErrorOr<BlockListResponse>>;
