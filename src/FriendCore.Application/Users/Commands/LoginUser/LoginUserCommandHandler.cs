using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Users.Commands.LoginUser;

internal sealed class LoginUserCommandHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    IRefreshTokenService refreshTokenService,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<LoginUserCommand, ErrorOr<AuthTokens>>
{
    public async Task<ErrorOr<AuthTokens>> Handle(LoginUserCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await userRepository.GetByEmailAsync(email, cancellationToken);

        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
            return UserErrors.InvalidCredentials;

        if (user.IsBanned)
            return UserErrors.AccountBanned;

        // §13.2: a temporary suspension blocks login until it expires.
        if (user.IsSuspended(dateTimeProvider.UtcNow))
            return UserErrors.AccountSuspended;

        user.RecordLogin(dateTimeProvider.UtcNow);
        await userRepository.UpdateAsync(user, cancellationToken);

        var token = jwtTokenService.GenerateAccessToken(user);
        var refresh = await refreshTokenService.IssueAsync(user.Id, cancellationToken);

        var auth = new AuthResponse(
            token.AccessToken,
            token.ExpiresAt,
            user.Id.ToString(),
            user.Username,
            user.DisplayName,
            user.IsVerified);
        return new AuthTokens(auth, refresh.Token, refresh.ExpiresAt);
    }
}