using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Common;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Users.Commands.SetNotificationSnooze;

internal sealed class SetNotificationSnoozeCommandHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<SetNotificationSnoozeCommand, ErrorOr<NotificationSnoozeResponse>>
{
    // A break, not an off-switch: anything beyond a day out is rejected so a forgotten
    // snooze can't silently eat notifications indefinitely.
    private static readonly TimeSpan MaxSnooze = TimeSpan.FromHours(24);

    public async Task<ErrorOr<NotificationSnoozeResponse>> Handle(
        SetNotificationSnoozeCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return CommonErrors.Unauthorized;

        var now = dateTimeProvider.UtcNow;
        if (request.Until <= now || request.Until - now > MaxSnooze)
            return UserErrors.InvalidSnoozeWindow;

        var preferences = await userRepository.GetPreferencesAsync(userId, cancellationToken);
        if (preferences is null)
            return UserErrors.NotFound;

        preferences.SetSnoozedUntil(request.Until, now);
        await userRepository.UpdatePreferencesAsync(cancellationToken);
        return new NotificationSnoozeResponse(preferences.SnoozedUntilUtc);
    }
}
