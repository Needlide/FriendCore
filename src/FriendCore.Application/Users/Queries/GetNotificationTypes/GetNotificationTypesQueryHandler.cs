using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Common;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Users.Queries.GetNotificationTypes;

internal sealed class GetNotificationTypesQueryHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository)
    : IRequestHandler<GetNotificationTypesQuery, ErrorOr<NotificationTypesResponse>>
{
    public async Task<ErrorOr<NotificationTypesResponse>> Handle(
        GetNotificationTypesQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return CommonErrors.Unauthorized;

        var preferences = await userRepository.GetPreferencesAsync(userId, cancellationToken);
        if (preferences is null)
            return UserErrors.NotFound;

        return new NotificationTypesResponse(preferences.MutedNotificationTypes);
    }
}
