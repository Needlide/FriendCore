using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Common;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Users.Queries.GetMutes;

internal sealed class GetMutesQueryHandler : IRequestHandler<GetMutesQuery, ErrorOr<MuteListResponse>>
{
    private readonly IMuteRepository _muteRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUserService _currentUserService;

    public GetMutesQueryHandler(
        IMuteRepository muteRepository,
        IUserRepository userRepository,
        ICurrentUserService currentUserService)
    {
        _muteRepository = muteRepository;
        _userRepository = userRepository;
        _currentUserService = currentUserService;
    }

    public async Task<ErrorOr<MuteListResponse>> Handle(
        GetMutesQuery request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId is null)
            return CommonErrors.Unauthorized;

        var mutedIds = await _muteRepository.GetMutedIdsAsync(userId.Value, cancellationToken);

        // Ignore blocks for the same reason as the block list: a mute can outlive a later
        // block, and the owner must still see who they muted in order to unmute them.
        var users = await _userRepository.GetByIdsIgnoringBlocksAsync(mutedIds, cancellationToken);
        var summaries = users
            .OrderBy(user => user.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(user => user.Username, StringComparer.OrdinalIgnoreCase)
            .Select(user => UserSummaryResponse.FromUser(user))
            .ToList();

        return new MuteListResponse(summaries);
    }
}
