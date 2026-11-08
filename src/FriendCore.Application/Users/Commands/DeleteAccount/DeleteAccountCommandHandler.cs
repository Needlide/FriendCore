using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Domain.Common;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Users.Commands.DeleteAccount;

internal sealed class DeleteAccountCommandHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository,
    IRefreshTokenService refreshTokenService,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<DeleteAccountCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(DeleteAccountCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return CommonErrors.Unauthorized;

        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
            return UserErrors.NotFound;

        user.Delete(dateTimeProvider.UtcNow);
        await userRepository.UpdateAsync(user, cancellationToken);

        // Every device logs out immediately; the access token expires on its own short clock.
        await refreshTokenService.RevokeAllForUserAsync(userId, cancellationToken);

        return Result.Success;
    }
}
