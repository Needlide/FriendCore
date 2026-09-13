using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Options;
using FriendCore.Application.Users.Email;
using FriendCore.Domain.Users;
using MediatR;
using Microsoft.Extensions.Options;

namespace FriendCore.Application.Users.Commands.RequestPasswordReset;

internal sealed class RequestPasswordResetCommandHandler(
    IUserRepository userRepository,
    IEmailHashService emailHashService,
    IPasswordResetTokenRepository tokenRepository,
    IEmailVerificationTokenHasher tokenHasher,
    IEmailService emailService,
    IDateTimeProvider dateTimeProvider,
    IOptions<EmailOptions> emailOptions)
    : IRequestHandler<RequestPasswordResetCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(
        RequestPasswordResetCommand request,
        CancellationToken cancellationToken)
    {
        // Blind by design (anti-enumeration): unknown username, hash mismatch, banned —
        // all return success. Only an exact username + email-hash match sends anything,
        // and only to the ENTERED address (no address is ever stored to send to).
        var slug = User.ToUsernameSlug(request.Username);
        var user = await userRepository.GetByUsernameSlugAsync(slug, cancellationToken);
        var email = User.NormalizeEmail(request.Email);

        if (user is not null && emailHashService.Hash(email) == user.EmailHash)
        {
            var options = emailOptions.Value;
            var now = dateTimeProvider.UtcNow;
            var rawToken = tokenHasher.GenerateToken();
            var tokenHash = tokenHasher.Hash(rawToken);

            await tokenRepository.RevokeActiveForUserAsync(user.Id, now, cancellationToken);

            var resetToken = PasswordResetToken.Create(
                user.Id,
                tokenHash,
                now,
                TimeSpan.FromHours(options.ResetTokenExpirationHours));

            await tokenRepository.AddAsync(resetToken, cancellationToken);

            var resetUrl = EmailContentBuilder.BuildResetUrl(options, rawToken);
            await emailService.SendAsync(
                EmailContentBuilder.PasswordReset(email, user.DisplayName, resetUrl, options),
                cancellationToken);
        }

        return Result.Success;
    }
}
