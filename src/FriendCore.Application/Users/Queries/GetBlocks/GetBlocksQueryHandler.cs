using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Users;
using FriendCore.Domain.Common;
using MediatR;

namespace FriendCore.Application.Users.Queries.GetBlocks;

internal sealed class GetBlocksQueryHandler : IRequestHandler<GetBlocksQuery, ErrorOr<BlockListResponse>>
{
    private readonly IBlockRepository _blockRepository;
    private readonly ICurrentUserService _currentUserService;

    public GetBlocksQueryHandler(IBlockRepository blockRepository, ICurrentUserService currentUserService)
    {
        _blockRepository = blockRepository;
        _currentUserService = currentUserService;
    }

    public async Task<ErrorOr<BlockListResponse>> Handle(GetBlocksQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId is null)
            return CommonErrors.Unauthorized;

        var counterpartIds = await _blockRepository.GetCounterpartIdsAsync(userId.Value, cancellationToken);
        return new BlockListResponse(counterpartIds.ToList());
    }
}
