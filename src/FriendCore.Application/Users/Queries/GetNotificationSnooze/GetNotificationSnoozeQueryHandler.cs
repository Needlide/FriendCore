using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Common;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Users.Queries.GetNotificationSnooze;

internal sealed class GetNotificationSnoozeQueryHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository)
    : IRequestHandler<GetNotificationSnoozeQuery, ErrorOr<NotificationSnoozeResponse>>
{
    public async Task<ErrorOr<NotificationSnoozeResponse>> Handle(
        GetNotificationSnoozeQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return CommonErrors.Unauthorized;

        var preferences = await userRepository.GetPreferencesAsync(userId, cancellationToken);
        if (preferences is null)
            return UserErrors.NotFound;

        return new NotificationSnoozeResponse(preferences.SnoozedUntilUtc);
    }
}
