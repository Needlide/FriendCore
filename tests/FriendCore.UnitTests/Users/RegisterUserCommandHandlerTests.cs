using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Users.Commands.RegisterUser;
using FriendCore.Domain.Users;
using NSubstitute;

namespace FriendCore.UnitTests.Users;

public class RegisterUserCommandHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsConflict_WhenEmailAlreadyExists()
    {
        var repository = Substitute.For<IUserRepository>();
        repository.ExistsByEmailAsync("taken@example.com", Arg.Any<CancellationToken>()).Returns(true);

        var handler = new RegisterUserCommandHandler(
            repository,
            Substitute.For<IPasswordHasher>(),
            Substitute.For<IJwtTokenService>(),
            Substitute.For<IDateTimeProvider>());

        var result = await handler.Handle(
            new RegisterUserCommand("newuser", "New User", "taken@example.com", "password123"),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains(result.Errors, error => error.Code == "User.EmailAlreadyInUse");
    }
}