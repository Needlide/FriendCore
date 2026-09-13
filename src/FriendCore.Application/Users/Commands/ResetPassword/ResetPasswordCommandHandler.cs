using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Domain.Common;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Users.Commands.ResetPassword;

internal sealed class ResetPasswordCommandHandler(
    IUserRepository userRepository,
    IPasswordResetTokenRepository tokenRepository,
    IEmailVerificationTokenHasher tokenHasher,
    IPasswordHasher passwordHasher,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<ResetPasswordCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(
        ResetPasswordCommand request,
        CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.UtcNow;
        var tokenHash = tokenHasher.Hash(request.Token);
        var token = await tokenRepository.GetActiveByTokenHashAsync(tokenHash, now, cancellationToken);
        if (token is null)
            return UserErrors.InvalidResetToken;

        var user = await userRepository.GetByIdAsync(token.UserId, cancellationToken);
        if (user is null)
            return UserErrors.InvalidResetToken;

        token.Consume(now);
        user.ChangePassword(passwordHasher.Hash(request.NewPassword), now);

        await tokenRepository.UpdateAsync(token, cancellationToken);
        await userRepository.UpdateAsync(user, cancellationToken);
        return Result.Success;
    }
}
