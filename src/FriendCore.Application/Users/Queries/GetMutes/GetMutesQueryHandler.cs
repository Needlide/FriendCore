using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Users;
using FriendCore.Domain.Common;
using MediatR;

namespace FriendCore.Application.Users.Queries.GetMutes;

internal sealed class GetMutesQueryHandler : IRequestHandler<GetMutesQuery, ErrorOr<MuteListResponse>>
{
    private readonly IMuteRepository _muteRepository;
    private readonly ICurrentUserService _currentUserService;

    public GetMutesQueryHandler(IMuteRepository muteRepository, ICurrentUserService currentUserService)
    {
        _muteRepository = muteRepository;
        _currentUserService = currentUserService;
    }

    public async Task<ErrorOr<MuteListResponse>> Handle(GetMutesQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId is null)
            return CommonErrors.Unauthorized;

        var mutedIds = await _muteRepository.GetMutedIdsAsync(userId.Value, cancellationToken);
        return new MuteListResponse(mutedIds.ToList());
    }
}
