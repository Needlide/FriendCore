using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using MediatR;

namespace FriendCore.Application.Users.Commands.Logout;

internal sealed class LogoutCommandHandler(IRefreshTokenService refreshTokenService)
    : IRequestHandler<LogoutCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        await refreshTokenService.RevokeAsync(request.RefreshToken, cancellationToken);
        return Result.Success;
    }
}
