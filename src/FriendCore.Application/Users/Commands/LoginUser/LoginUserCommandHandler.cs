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
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<LoginUserCommand, ErrorOr<AuthResponse>>
{
    public async Task<ErrorOr<AuthResponse>> Handle(LoginUserCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await userRepository.GetByEmailAsync(email, cancellationToken);

        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
            return UserErrors.InvalidCredentials;

        if (user.IsBanned)
            return UserErrors.AccountBanned;

        user.RecordLogin(dateTimeProvider.UtcNow);
        await userRepository.UpdateAsync(user, cancellationToken);

        var token = jwtTokenService.GenerateAccessToken(user);
        return new AuthResponse(
            token.AccessToken,
            token.ExpiresAt,
            user.Id.ToString(),
            user.Username,
            user.DisplayName);
    }
}