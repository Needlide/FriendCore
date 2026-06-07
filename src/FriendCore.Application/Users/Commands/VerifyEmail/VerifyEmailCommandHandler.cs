using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Users.Commands.VerifyEmail;

internal sealed class VerifyEmailCommandHandler(
    IEmailVerificationTokenRepository tokenRepository,
    IUserRepository userRepository,
    IEmailVerificationTokenHasher tokenHasher,
    IJwtTokenService jwtTokenService,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<VerifyEmailCommand, ErrorOr<AuthResponse>>
{
    public async Task<ErrorOr<AuthResponse>> Handle(VerifyEmailCommand request, CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.UtcNow;
        var tokenHash = tokenHasher.Hash(request.Token.Trim());
        var verificationToken = await tokenRepository.GetActiveByTokenHashAsync(tokenHash, now, cancellationToken);

        if (verificationToken is null || !verificationToken.IsActive(now))
            return UserErrors.InvalidVerificationToken;

        var user = await userRepository.GetByIdAsync(verificationToken.UserId, cancellationToken);
        if (user is null)
            return UserErrors.NotFound;

        if (user.IsVerified)
            return UserErrors.EmailAlreadyVerified;

        var verifyResult = user.MarkEmailVerified(now);
        if (verifyResult.IsError)
            return verifyResult.Errors;

        verificationToken.Consume(now);
        await tokenRepository.UpdateAsync(verificationToken, cancellationToken);
        await userRepository.UpdateAsync(user, cancellationToken);

        var accessToken = jwtTokenService.GenerateAccessToken(user);
        return new AuthResponse(
            accessToken.AccessToken,
            accessToken.ExpiresAt,
            user.Id.ToString(),
            user.Username,
            user.DisplayName,
            user.IsVerified);
    }
}
