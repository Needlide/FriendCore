using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Options;
using FriendCore.Application.Users.EventHandlers;
using FriendCore.Domain.Users;
using FriendCore.Domain.Users.Events;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace FriendCore.UnitTests.Users;

public class UserRegisteredEventHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly IEmailVerificationTokenRepository _tokenRepository = Substitute.For<IEmailVerificationTokenRepository>();
    private readonly IEmailVerificationTokenHasher _tokenHasher = Substitute.For<IEmailVerificationTokenHasher>();
    private readonly IEmailService _emailService = Substitute.For<IEmailService>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly IOptions<EmailOptions> _emailOptions = Options.Create(new EmailOptions());
    private readonly NullLogger<UserRegisteredEventHandler> _logger = NullLogger<UserRegisteredEventHandler>.Instance;

    public UserRegisteredEventHandlerTests()
    {
        _dateTimeProvider.UtcNow.Returns(Now);
        _tokenHasher.GenerateToken().Returns("raw-token");
        _tokenHasher.Hash("raw-token").Returns("hashed-token");
    }

    private UserRegisteredEventHandler CreateHandler() =>
        new(_tokenRepository, _tokenHasher, _emailService, _dateTimeProvider, _emailOptions, _logger);

    private static UserRegisteredEvent Event() =>
        new(Ulid.NewUlid(), "alice@example.com", "Alice", "alice", Now);

    [Fact]
    public async Task Handle_PersistsTokenAndSendsWelcomeAndVerificationEmails()
    {
        var notification = Event();

        await CreateHandler().Handle(notification, CancellationToken.None);

        await _tokenRepository.Received(1).RevokeActiveForUserAsync(notification.UserId, Now, Arg.Any<CancellationToken>());
        await _tokenRepository.Received(1).AddAsync(
            Arg.Is<EmailVerificationToken>(t => t.UserId == notification.UserId && t.TokenHash == "hashed-token"),
            Arg.Any<CancellationToken>());
        await _emailService.Received(2).SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
        await _emailService.Received(1).SendAsync(
            Arg.Is<EmailMessage>(m => m.Subject == "Welcome to FriendCore"),
            Arg.Any<CancellationToken>());
        await _emailService.Received(1).SendAsync(
            Arg.Is<EmailMessage>(m => m.Subject == "Verify your FriendCore email"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DoesNotThrow_WhenEmailServiceFails()
    {
        _emailService
            .SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("smtp down"));

        var exception = await Record.ExceptionAsync(() =>
            CreateHandler().Handle(Event(), CancellationToken.None));

        Assert.Null(exception);
    }
}
