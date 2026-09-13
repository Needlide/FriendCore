using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Options;
using FriendCore.Application.Users.Commands.RequestPasswordReset;
using FriendCore.Domain.Users;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace FriendCore.UnitTests.Users;

public class RequestPasswordResetCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 2, 9, 0, 0, TimeSpan.Zero);

    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IEmailHashService _emailHashService = Substitute.For<IEmailHashService>();
    private readonly IPasswordResetTokenRepository _tokenRepository = Substitute.For<IPasswordResetTokenRepository>();
    private readonly IEmailVerificationTokenHasher _tokenHasher = Substitute.For<IEmailVerificationTokenHasher>();
    private readonly IEmailService _emailService = Substitute.For<IEmailService>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly IOptions<EmailOptions> _emailOptions = Options.Create(new EmailOptions());

    public RequestPasswordResetCommandHandlerTests()
    {
        _dateTimeProvider.UtcNow.Returns(Now);
        _tokenHasher.GenerateToken().Returns("raw-token");
        _tokenHasher.Hash("raw-token").Returns("hashed-token");
        _emailHashService.Hash("alice@example.com").Returns(UserTestFactory.HashEmail("alice@example.com"));
    }

    private RequestPasswordResetCommandHandler CreateHandler() =>
        new(_userRepository, _emailHashService, _tokenRepository, _tokenHasher, _emailService,
            _dateTimeProvider, _emailOptions);

    [Fact]
    public async Task Handle_SucceedsBlindly_WhenUserUnknown()
    {
        _userRepository.GetByUsernameSlugAsync("ghost", Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var result = await CreateHandler().Handle(
            new RequestPasswordResetCommand("ghost", "ghost@example.com"), CancellationToken.None);

        Assert.False(result.IsError);
        await _emailService.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SucceedsBlindly_WhenEmailDoesNotMatch()
    {
        var user = UserTestFactory.Create();
        _userRepository.GetByUsernameSlugAsync(user.UsernameSlug, Arg.Any<CancellationToken>()).Returns(user);
        _emailHashService.Hash("wrong@example.com").Returns("hash:nope");

        var result = await CreateHandler().Handle(
            new RequestPasswordResetCommand(user.Username, "wrong@example.com"), CancellationToken.None);

        Assert.False(result.IsError);
        await _emailService.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
        await _tokenRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task Handle_SendsResetLink_ToEnteredAddress_OnMatch()
    {
        var user = UserTestFactory.Create();
        _userRepository.GetByUsernameSlugAsync(user.UsernameSlug, Arg.Any<CancellationToken>()).Returns(user);

        var result = await CreateHandler().Handle(
            new RequestPasswordResetCommand(user.Username, "alice@example.com"), CancellationToken.None);

        Assert.False(result.IsError);
        await _tokenRepository.Received(1).AddAsync(
            Arg.Is<PasswordResetToken>(t => t.UserId == user.Id && t.TokenHash == "hashed-token"),
            Arg.Any<CancellationToken>());
        await _emailService.Received(1).SendAsync(
            Arg.Is<EmailMessage>(m => m.To == "alice@example.com"),
            Arg.Any<CancellationToken>());
    }
}
