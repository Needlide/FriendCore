using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Options;
using FriendCore.Application.Users.Email;
using FriendCore.Domain.Users;
using FriendCore.Domain.Users.Events;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FriendCore.Application.Users.EventHandlers;

internal sealed class UserRegisteredEventHandler(
    IEmailVerificationTokenRepository tokenRepository,
    IEmailVerificationTokenHasher tokenHasher,
    IEmailService emailService,
    IDateTimeProvider dateTimeProvider,
    IOptions<EmailOptions> emailOptions,
    ILogger<UserRegisteredEventHandler> logger) : INotificationHandler<UserRegisteredEvent>
{
    public async Task Handle(UserRegisteredEvent notification, CancellationToken cancellationToken)
    {
        var options = emailOptions.Value;
        var now = dateTimeProvider.UtcNow;

        try
        {
            var rawToken = tokenHasher.GenerateToken();
            var tokenHash = tokenHasher.Hash(rawToken);
            var validity = TimeSpan.FromHours(options.VerificationTokenExpirationHours);

            await tokenRepository.RevokeActiveForUserAsync(notification.UserId, now, cancellationToken);

            var verificationToken = EmailVerificationToken.Create(
                notification.UserId,
                tokenHash,
                now,
                validity);

            await tokenRepository.AddAsync(verificationToken, cancellationToken);

            var verificationUrl = EmailContentBuilder.BuildVerificationUrl(options, rawToken);

            await emailService.SendAsync(
                EmailContentBuilder.Welcome(notification.Email, notification.DisplayName, options),
                cancellationToken);

            await emailService.SendAsync(
                EmailContentBuilder.Verification(
                    notification.Email,
                    notification.DisplayName,
                    verificationUrl,
                    options),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to send registration emails for user {UserId}",
                notification.UserId);
        }
    }
}