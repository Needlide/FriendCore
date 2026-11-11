using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Users;
using FriendCore.Domain.Common;
using MediatR;

namespace FriendCore.Application.Users.Queries.GetPreferences;

internal sealed class GetPreferencesQueryHandler : IRequestHandler<GetPreferencesQuery, ErrorOr<PreferencesResponse>>
{
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUserService _currentUserService;

    public GetPreferencesQueryHandler(ICurrentUserService currentUserService, IUserRepository userRepository)
    {
        _userRepository = userRepository;
        _currentUserService = currentUserService;
    }

    public async Task<ErrorOr<PreferencesResponse>> Handle(GetPreferencesQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId is null)
            return CommonErrors.Unauthorized;

        var user = await _userRepository.GetByIdAsync(userId.Value, cancellationToken);
        if (user is null)
            return UserErrors.NotFound;

        var preferences = await _userRepository.GetPreferencesAsync(userId.Value, cancellationToken);
        if (preferences is null)
            return UserErrors.NotFound;

        return new PreferencesResponse(
            preferences.HideFlashingMedia,
            preferences.HideIntenseMotionMedia,
            preferences.HideArachnidMedia,
            preferences.ReduceMotion,
            preferences.DisableAutoplay,
            preferences.ReduceVisualNoise,
            preferences.SendMessageOnEnter,
            user.ContentPreference.ToString());
    }
}
