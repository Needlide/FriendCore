using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Options;
using FriendCore.Application.Users.Commands.ResendVerificationEmail;
using FriendCore.Domain.Users;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace FriendCore.UnitTests.Users;

public class ResendVerificationEmailCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 2, 9, 0, 0, TimeSpan.Zero);

    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly IOptions<EmailOptions> _emailOptions = Options.Create(new EmailOptions());
    private readonly IEmailService _emailService = Substitute.For<IEmailService>();
    private readonly IEmailHashService _emailHashService = Substitute.For<IEmailHashService>();
    private readonly IEmailVerificationTokenHasher _tokenHasher = Substitute.For<IEmailVerificationTokenHasher>();

    private readonly IEmailVerificationTokenRepository _tokenRepository =
        Substitute.For<IEmailVerificationTokenRepository>();

    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();

    public ResendVerificationEmailCommandHandlerTests()
    {
        _dateTimeProvider.UtcNow.Returns(Now);
        _tokenHasher.GenerateToken().Returns("raw-token");
        _tokenHasher.Hash("raw-token").Returns("hashed-token");
        // Factory users are created for alice@example.com — mirror that mapping here.
        _emailHashService.Hash("alice@example.com").Returns(UserTestFactory.HashEmail("alice@example.com"));
    }

    private ResendVerificationEmailCommandHandler CreateHandler()
    {
        return new ResendVerificationEmailCommandHandler(
            _currentUserService,
            _userRepository,
            _emailHashService,
            _tokenRepository,
            _tokenHasher,
            _emailService,
            _dateTimeProvider,
            _emailOptions);
    }

    [Fact]
    public async Task Handle_ReturnsUnauthorized_WhenNoCurrentUser()
    {
        _currentUserService.UserId.Returns((Ulid?)null);

        var result = await CreateHandler().Handle(
            new ResendVerificationEmailCommand("alice@example.com"), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("User.Unauthorized", result.FirstError.Code);
        await _emailService.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ReturnsNotFound_WhenUserMissing()
    {
        var userId = Ulid.NewUlid();
        _currentUserService.UserId.Returns(userId);
        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns((User?)null);

        var result = await CreateHandler().Handle(
            new ResendVerificationEmailCommand("alice@example.com"), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(UserErrors.NotFound.Code, result.FirstError.Code);
    }

    [Fact]
    public async Task Handle_ReturnsAlreadyVerified_WhenUserVerified()
    {
        var user = UserTestFactory.CreateVerified();
        _currentUserService.UserId.Returns(user.Id);
        _userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var result = await CreateHandler().Handle(
            new ResendVerificationEmailCommand("alice@example.com"), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(UserErrors.EmailAlreadyVerified.Code, result.FirstError.Code);
        await _emailService.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SucceedsBlindly_WhenEmailDoesNotMatch()
    {
        // Wrong address: blind success, nothing sent, nothing stored — the caller learns nothing.
        var user = UserTestFactory.Create();
        _currentUserService.UserId.Returns(user.Id);
        _userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        _emailHashService.Hash("wrong@example.com").Returns("hash:nope");

        var result = await CreateHandler().Handle(
            new ResendVerificationEmailCommand("wrong@example.com"), CancellationToken.None);

        Assert.False(result.IsError);
        await _emailService.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
        await _tokenRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task Handle_RevokesAddsTokenAndSendsEmail_OnSuccess()
    {
        var user = UserTestFactory.Create();
        _currentUserService.UserId.Returns(user.Id);
        _userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var result = await CreateHandler().Handle(
            new ResendVerificationEmailCommand("alice@example.com"), CancellationToken.None);

        Assert.False(result.IsError);
        await _tokenRepository.Received(1).RevokeActiveForUserAsync(user.Id, Now, Arg.Any<CancellationToken>());
        await _tokenRepository.Received(1).AddAsync(
            Arg.Is<EmailVerificationToken>(t => t.UserId == user.Id && t.TokenHash == "hashed-token"),
            Arg.Any<CancellationToken>());
        await _emailService.Received(1).SendAsync(
            Arg.Is<EmailMessage>(m => m.To == "alice@example.com"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_RevokesExistingTokensBeforeAddingNew()
    {
        var user = UserTestFactory.Create();
        _currentUserService.UserId.Returns(user.Id);
        _userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        await CreateHandler().Handle(
            new ResendVerificationEmailCommand("alice@example.com"), CancellationToken.None);

        Received.InOrder(() =>
        {
            _tokenRepository.RevokeActiveForUserAsync(user.Id, Now, Arg.Any<CancellationToken>());
            _tokenRepository.AddAsync(Arg.Any<EmailVerificationToken>(), Arg.Any<CancellationToken>());
        });
    }
}
