using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Users.Commands.Logout;
using NSubstitute;

namespace FriendCore.UnitTests.Users;

public class LogoutCommandHandlerTests
{
    private readonly IRefreshTokenService _refreshTokenService = Substitute.For<IRefreshTokenService>();

    private LogoutCommandHandler CreateHandler() => new(_refreshTokenService);

    [Fact]
    public async Task Handle_RevokesThePresentedToken_AndSucceeds()
    {
        var result = await CreateHandler().Handle(new LogoutCommand("raw"), CancellationToken.None);

        Assert.False(result.IsError);
        await _refreshTokenService.Received(1).RevokeAsync("raw", Arg.Any<CancellationToken>());
    }
}
