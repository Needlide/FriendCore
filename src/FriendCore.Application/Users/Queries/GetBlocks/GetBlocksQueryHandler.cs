using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Common;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Users.Queries.GetBlocks;

internal sealed class GetBlocksQueryHandler : IRequestHandler<GetBlocksQuery, ErrorOr<BlockListResponse>>
{
    private readonly IBlockRepository _blockRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUserService _currentUserService;

    public GetBlocksQueryHandler(
        IBlockRepository blockRepository,
        IUserRepository userRepository,
        ICurrentUserService currentUserService)
    {
        _blockRepository = blockRepository;
        _userRepository = userRepository;
        _currentUserService = currentUserService;
    }

    public async Task<ErrorOr<BlockListResponse>> Handle(
        GetBlocksQuery request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId is null)
            return CommonErrors.Unauthorized;

        var blockedIds = await _blockRepository.GetBlockedIdsAsync(userId.Value, cancellationToken);

        // The block filter is exactly what hides these users from every other query, so the
        // management list must opt out of it; otherwise it would return ids with no profile
        // data and the UI could not show a name to unblock.
        var users = await _userRepository.GetByIdsIgnoringBlocksAsync(blockedIds, cancellationToken);
        var summaries = users
            .OrderBy(user => user.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(user => user.Username, StringComparer.OrdinalIgnoreCase)
            .Select(user => UserSummaryResponse.FromUser(user))
            .ToList();

        return new BlockListResponse(summaries);
    }
}
