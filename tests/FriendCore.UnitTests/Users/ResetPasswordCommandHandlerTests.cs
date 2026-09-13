using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Users.Commands.ResetPassword;
using FriendCore.Domain.Users;
using NSubstitute;

namespace FriendCore.UnitTests.Users;

public class ResetPasswordCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 2, 9, 0, 0, TimeSpan.Zero);

    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IPasswordResetTokenRepository _tokenRepository = Substitute.For<IPasswordResetTokenRepository>();
    private readonly IEmailVerificationTokenHasher _tokenHasher = Substitute.For<IEmailVerificationTokenHasher>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();

    public ResetPasswordCommandHandlerTests()
    {
        _dateTimeProvider.UtcNow.Returns(Now);
        _passwordHasher.Hash("new-password-123").Returns("new-hash");
    }

    private ResetPasswordCommandHandler CreateHandler() =>
        new(_userRepository, _tokenRepository, _tokenHasher, _passwordHasher, _dateTimeProvider);

    [Fact]
    public async Task Handle_RejectsUnknownToken()
    {
        _tokenHasher.Hash("bogus").Returns("hash:bogus");
        _tokenRepository.GetActiveByTokenHashAsync("hash:bogus", Now, Arg.Any<CancellationToken>())
            .Returns((PasswordResetToken?)null);

        var result = await CreateHandler().Handle(
            new ResetPasswordCommand("bogus", "new-password-123"), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("User.InvalidResetToken", result.FirstError.Code);
    }

    [Fact]
    public async Task Handle_ConsumesToken_AndChangesPassword()
    {
        var user = UserTestFactory.Create();
        var token = PasswordResetToken.Create(user.Id, "hash:raw", Now.AddHours(-1), TimeSpan.FromHours(2));
        _tokenHasher.Hash("raw").Returns("hash:raw");
        _tokenRepository.GetActiveByTokenHashAsync("hash:raw", Now, Arg.Any<CancellationToken>())
            .Returns(token);
        _userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var result = await CreateHandler().Handle(
            new ResetPasswordCommand("raw", "new-password-123"), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal("new-hash", user.PasswordHash);
        Assert.NotNull(token.ConsumedAt);
        await _tokenRepository.Received(1).UpdateAsync(token, Arg.Any<CancellationToken>());
        await _userRepository.Received(1).UpdateAsync(user, Arg.Any<CancellationToken>());
    }
}
