using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Users.Commands.RefreshToken;

internal sealed class RefreshTokenCommandHandler(
    IRefreshTokenService refreshTokenService,
    IUserRepository userRepository,
    IJwtTokenService jwtTokenService,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<RefreshTokenCommand, ErrorOr<AuthTokens>>
{
    public async Task<ErrorOr<AuthTokens>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var rotated = await refreshTokenService.RotateAsync(request.RefreshToken, cancellationToken);
        if (rotated.IsError)
            return rotated.Errors;

        var user = await userRepository.GetByIdAsync(rotated.Value.UserId, cancellationToken);

        // A refresh must not extend a banned/suspended (or deleted) account's session — revoke the
        // whole family so the just-rotated token is killed too, closing the access-token window.
        if (user is null || user.IsBanned || user.IsSuspended(dateTimeProvider.UtcNow))
        {
            await refreshTokenService.RevokeAllForUserAsync(rotated.Value.UserId, cancellationToken);
            return user is null ? UserErrors.InvalidRefreshToken
                : user.IsBanned ? UserErrors.AccountBanned
                : UserErrors.AccountSuspended;
        }

        var token = jwtTokenService.GenerateAccessToken(user);
        var auth = new AuthResponse(
            token.AccessToken,
            token.ExpiresAt,
            user.Id.ToString(),
            user.Username,
            user.DisplayName,
            user.IsVerified);

        return new AuthTokens(auth, rotated.Value.Token, rotated.Value.ExpiresAt);
    }
}
