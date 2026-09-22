using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Common;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Users.Commands.UpdateNotificationTypes;

internal sealed class UpdateNotificationTypesCommandHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<UpdateNotificationTypesCommand, ErrorOr<NotificationTypesResponse>>
{
    public async Task<ErrorOr<NotificationTypesResponse>> Handle(
        UpdateNotificationTypesCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return CommonErrors.Unauthorized;

        var preferences = await userRepository.GetPreferencesAsync(userId, cancellationToken);
        if (preferences is null)
            return UserErrors.NotFound;

        preferences.SetMutedNotificationTypes(request.MutedTypes, dateTimeProvider.UtcNow);
        await userRepository.UpdatePreferencesAsync(cancellationToken);
        return new NotificationTypesResponse(preferences.MutedNotificationTypes);
    }
}
