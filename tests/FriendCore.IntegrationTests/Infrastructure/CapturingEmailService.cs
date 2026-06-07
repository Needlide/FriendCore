using System.Collections.Concurrent;
using FriendCore.Application.Common.Interfaces;

namespace FriendCore.IntegrationTests.Infrastructure;

/// <summary>
///     Test double for <see cref="IEmailService" /> that records every sent message instead of
///     calling Resend. The raw verification token only ever leaves the system inside the email URL,
///     so capturing the message is how the test recovers it (the DB stores only the hash).
/// </summary>
public sealed class CapturingEmailService : IEmailService
{
    private readonly ConcurrentQueue<EmailMessage> _messages = new();

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        _messages.Enqueue(message);
        return Task.CompletedTask;
    }

    public IEnumerable<EmailMessage> For(string recipient)
    {
        return _messages.Where(m => string.Equals(m.To, recipient, StringComparison.OrdinalIgnoreCase));
    }
}