using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Options;
using FriendCore.Application.Users.Email;
using FriendCore.Domain.Users;
using MediatR;
using Microsoft.Extensions.Options;

namespace FriendCore.Application.Users.Commands.ResendVerificationEmail;

internal sealed class ResendVerificationEmailCommandHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository,
    IEmailVerificationTokenRepository tokenRepository,
    IEmailVerificationTokenHasher tokenHasher,
    IEmailService emailService,
    IDateTimeProvider dateTimeProvider,
    IOptions<EmailOptions> emailOptions)
    : IRequestHandler<ResendVerificationEmailCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(
        ResendVerificationEmailCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return Error.Unauthorized("User.Unauthorized", "Authentication is required.");

        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
            return UserErrors.NotFound;

        if (user.IsVerified)
            return UserErrors.EmailAlreadyVerified;

        var options = emailOptions.Value;
        var now = dateTimeProvider.UtcNow;
        var rawToken = tokenHasher.GenerateToken();
        var tokenHash = tokenHasher.Hash(rawToken);

        await tokenRepository.RevokeActiveForUserAsync(userId, now, cancellationToken);

        var verificationToken = EmailVerificationToken.Create(
            userId,
            tokenHash,
            now,
            TimeSpan.FromHours(options.VerificationTokenExpirationHours));

        await tokenRepository.AddAsync(verificationToken, cancellationToken);

        var verificationUrl = EmailContentBuilder.BuildVerificationUrl(options, rawToken);
        await emailService.SendAsync(
            EmailContentBuilder.Verification(user.Email, user.DisplayName, verificationUrl, options),
            cancellationToken);

        return Result.Success;
    }
}
