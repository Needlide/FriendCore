using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Domain.Common;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Users.Commands.UnmuteUser;

internal sealed class UnmuteUserCommandHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository,
    IMuteRepository muteRepository)
    : IRequestHandler<UnmuteUserCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(UnmuteUserCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return CommonErrors.Unauthorized;

        var slug = User.ToUsernameSlug(request.Username);
        var target = await userRepository.GetByUsernameSlugAsync(slug, cancellationToken);
        if (target is null)
            return UserErrors.NotFound;

        // Idempotent: unmuting someone you haven't muted is a no-op.
        await muteRepository.RemoveAsync(userId, target.Id, cancellationToken);
        return Result.Success;
    }
}
