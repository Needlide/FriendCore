using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Users.Commands.VerifyEmail;
using FriendCore.Domain.Users;
using NSubstitute;

namespace FriendCore.UnitTests.Users;

public class VerifyEmailCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 2, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Expires = Now.AddHours(24);
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly IJwtTokenService _jwtTokenService = Substitute.For<IJwtTokenService>();
    private readonly IEmailVerificationTokenHasher _tokenHasher = Substitute.For<IEmailVerificationTokenHasher>();
    private readonly IRefreshTokenService _refreshTokenService = Substitute.For<IRefreshTokenService>();

    private readonly IEmailVerificationTokenRepository _tokenRepository =
        Substitute.For<IEmailVerificationTokenRepository>();

    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();

    public VerifyEmailCommandHandlerTests()
    {
        _dateTimeProvider.UtcNow.Returns(Now);
        _tokenHasher.Hash(Arg.Any<string>()).Returns(call => $"hash::{call.Arg<string>()}");
        _jwtTokenService.GenerateAccessToken(Arg.Any<User>())
            .Returns(new AuthTokenResult("access-token", Now.AddHours(1)));
        _refreshTokenService.IssueAsync(Arg.Any<Ulid>(), Arg.Any<CancellationToken>())
            .Returns(new IssuedRefreshToken("refresh-token", Now.AddDays(60)));
    }

    private VerifyEmailCommandHandler CreateHandler()
    {
        return new VerifyEmailCommandHandler(_tokenRepository, _userRepository, _tokenHasher, _jwtTokenService,
            _refreshTokenService, _dateTimeProvider);
    }

    [Fact]
    public async Task Handle_ReturnsInvalidToken_WhenTokenNotFound()
    {
        _tokenRepository
            .GetActiveByTokenHashAsync(Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns((EmailVerificationToken?)null);

        var result = await CreateHandler().Handle(new VerifyEmailCommand("missing"), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(UserErrors.InvalidVerificationToken.Code, result.FirstError.Code);
    }

    [Fact]
    public async Task Handle_ReturnsInvalidToken_WhenTokenExpired()
    {
        var expired =
            EmailVerificationToken.Create(Ulid.NewUlid(), "hash::tok", Now.AddHours(-48), TimeSpan.FromHours(24));
        _tokenRepository
            .GetActiveByTokenHashAsync(Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(expired);

        var result = await CreateHandler().Handle(new VerifyEmailCommand("tok"), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(UserErrors.InvalidVerificationToken.Code, result.FirstError.Code);
    }

    [Fact]
    public async Task Handle_ReturnsNotFound_WhenUserMissing()
    {
        var token = EmailVerificationToken.Create(Ulid.NewUlid(), "hash::tok", Now, TimeSpan.FromHours(24));
        _tokenRepository
            .GetActiveByTokenHashAsync(Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(token);
        _userRepository.GetByIdAsync(token.UserId, Arg.Any<CancellationToken>()).Returns((User?)null);

        var result = await CreateHandler().Handle(new VerifyEmailCommand("tok"), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(UserErrors.NotFound.Code, result.FirstError.Code);
    }

    [Fact]
    public async Task Handle_ReturnsAlreadyVerified_WhenUserAlreadyVerified()
    {
        var user = UserTestFactory.CreateVerified();
        var token = EmailVerificationToken.Create(user.Id, "hash::tok", Now, TimeSpan.FromHours(24));
        _tokenRepository
            .GetActiveByTokenHashAsync(Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(token);
        _userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var result = await CreateHandler().Handle(new VerifyEmailCommand("tok"), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(UserErrors.EmailAlreadyVerified.Code, result.FirstError.Code);
    }

    [Fact]
    public async Task Handle_VerifiesUserConsumesTokenAndReturnsAuthResponse_OnSuccess()
    {
        var user = UserTestFactory.Create();
        var token = EmailVerificationToken.Create(user.Id, "hash::tok", Now, TimeSpan.FromHours(24));
        _tokenRepository.GetActiveByTokenHashAsync("hash::tok", Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(token);
        _userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var result = await CreateHandler().Handle(new VerifyEmailCommand("tok"), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal("access-token", result.Value.Auth.AccessToken);
        Assert.True(result.Value.Auth.IsEmailVerified);
        Assert.Equal(user.Username, result.Value.Auth.Username);

        Assert.True(user.IsVerified);
        Assert.Equal(Now, token.ConsumedAt);
        await _tokenRepository.Received(1).UpdateAsync(token, Arg.Any<CancellationToken>());
        await _userRepository.Received(1).UpdateAsync(user, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_TrimsTokenBeforeHashing()
    {
        var user = UserTestFactory.Create();
        var token = EmailVerificationToken.Create(user.Id, "hash::tok", Now, TimeSpan.FromHours(24));
        _tokenRepository
            .GetActiveByTokenHashAsync(Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(token);
        _userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        await CreateHandler().Handle(new VerifyEmailCommand("  tok  "), CancellationToken.None);

        _tokenHasher.Received(1).Hash("tok");
    }
}