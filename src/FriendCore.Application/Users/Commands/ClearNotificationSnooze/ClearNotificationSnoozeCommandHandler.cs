using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Common;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Users.Commands.ClearNotificationSnooze;

internal sealed class ClearNotificationSnoozeCommandHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<ClearNotificationSnoozeCommand, ErrorOr<NotificationSnoozeResponse>>
{
    public async Task<ErrorOr<NotificationSnoozeResponse>> Handle(
        ClearNotificationSnoozeCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return CommonErrors.Unauthorized;

        var preferences = await userRepository.GetPreferencesAsync(userId, cancellationToken);
        if (preferences is null)
            return UserErrors.NotFound;

        preferences.SetSnoozedUntil(null, dateTimeProvider.UtcNow);
        await userRepository.UpdatePreferencesAsync(cancellationToken);
        return new NotificationSnoozeResponse(null);
    }
}
