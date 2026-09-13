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
    IEmailHashService emailHashService,
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

        // The address is supplied per request and verified against the stored hash before
        // anything is sent: without a match there is nowhere to send TO, so the result is
        // a blind success that reveals nothing about which address (if any) is registered.
        var email = User.NormalizeEmail(request.Email);
        if (emailHashService.Hash(email) != user.EmailHash)
            return Result.Success;

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
            EmailContentBuilder.Verification(email, user.DisplayName, verificationUrl, options),
            cancellationToken);

        return Result.Success;
    }
}
