using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Application.Common.Options;
using FriendCore.Domain.Users;
using MediatR;
using Microsoft.Extensions.Options;

namespace FriendCore.Application.Users.Commands.RegisterUser;

internal sealed class RegisterUserCommandHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    IRefreshTokenService refreshTokenService,
    IOptions<SeedingOptions> seedingOptions,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<RegisterUserCommand, ErrorOr<AuthTokens>>
{
    public async Task<ErrorOr<AuthTokens>> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var slug = User.ToUsernameSlug(request.Username);

        if (await userRepository.ExistsByEmailAsync(email, cancellationToken))
            return UserErrors.EmailAlreadyInUse;

        if (await userRepository.ExistsByUsernameSlugAsync(slug, cancellationToken))
            return UserErrors.UsernameAlreadyInUse;

        var now = dateTimeProvider.UtcNow;
        var passwordHash = passwordHasher.Hash(request.Password);

        var userResult = User.Register(
            request.Username,
            request.DisplayName,
            email,
            passwordHash,
            now);

        if (userResult.IsError)
            return userResult.Errors;

        var user = userResult.Value;

        // Bootstrap admin (§ deployment): a configured email is granted Admin on registration, so
        // the first admin can be created on a fresh database without manual SQL. Empty by default.
        if (seedingOptions.Value.AdminEmails.Any(seed =>
                string.Equals(seed.Trim(), email, StringComparison.OrdinalIgnoreCase)))
            user.AssignRole(UserRole.Admin);

        var preferences = UserPreferences.CreateDefault(user.Id, now);

        await userRepository.AddAsync(user, preferences, cancellationToken);

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