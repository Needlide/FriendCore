using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Application.Common.Options;
using FriendCore.Application.Users.Commands.RegisterUser;
using FriendCore.Domain.Users;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace FriendCore.UnitTests.Users;

public class RegisterUserCommandHandlerTests
{
    private readonly IUserRepository _repository = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly IJwtTokenService _jwtTokenService = Substitute.For<IJwtTokenService>();
    private readonly IRefreshTokenService _refreshTokenService = Substitute.For<IRefreshTokenService>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private SeedingOptions _seeding = new();

    public RegisterUserCommandHandlerTests()
    {
        _passwordHasher.Hash(Arg.Any<string>()).Returns("hashed");
        _jwtTokenService.GenerateAccessToken(Arg.Any<User>())
            .Returns(new AuthTokenResult("token", new DateTimeOffset(2026, 6, 16, 9, 0, 0, TimeSpan.Zero)));
        _refreshTokenService.IssueAsync(Arg.Any<Ulid>(), Arg.Any<CancellationToken>())
            .Returns(new IssuedRefreshToken("refresh", new DateTimeOffset(2026, 8, 16, 9, 0, 0, TimeSpan.Zero)));
        _dateTimeProvider.UtcNow.Returns(new DateTimeOffset(2026, 6, 16, 9, 0, 0, TimeSpan.Zero));
    }

    private RegisterUserCommandHandler CreateHandler() =>
        new(_repository, _passwordHasher, _jwtTokenService, _refreshTokenService, Options.Create(_seeding),
            _dateTimeProvider);

    [Fact]
    public async Task Handle_ReturnsConflict_WhenEmailAlreadyExists()
    {
        _repository.ExistsByEmailAsync("taken@example.com", Arg.Any<CancellationToken>()).Returns(true);

        var result = await CreateHandler().Handle(
            new RegisterUserCommand("newuser", "New User", "taken@example.com", "password123"),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains(result.Errors, error => error.Code == "User.EmailAlreadyInUse");
    }

    [Fact]
    public async Task Handle_RegistersAsUser_ByDefault()
    {
        User? added = null;
        await _repository.AddAsync(
            Arg.Do<User>(u => added = u), Arg.Any<UserPreferences>(), Arg.Any<CancellationToken>());

        var result = await CreateHandler().Handle(
            new RegisterUserCommand("alice", "Alice", "alice@example.com", "password123"), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(UserRole.User, added!.Role);
    }

    [Fact]
    public async Task Handle_GrantsAdmin_WhenEmailIsInBootstrapList()
    {
        // §deployment bootstrap: a configured email becomes Admin on registration (case-insensitive).
        _seeding = new SeedingOptions { AdminEmails = ["Owner@Example.com"] };
        User? added = null;
        await _repository.AddAsync(
            Arg.Do<User>(u => added = u), Arg.Any<UserPreferences>(), Arg.Any<CancellationToken>());

        var result = await CreateHandler().Handle(
            new RegisterUserCommand("owner", "Owner", "owner@example.com", "password123"), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(UserRole.Admin, added!.Role);
    }
}
