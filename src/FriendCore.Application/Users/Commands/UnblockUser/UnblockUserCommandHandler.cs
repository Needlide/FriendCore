using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Domain.Common;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Users.Commands.UnblockUser;

internal sealed class UnblockUserCommandHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository,
    IBlockRepository blockRepository)
    : IRequestHandler<UnblockUserCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(UnblockUserCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return CommonErrors.Unauthorized;

        var slug = User.ToUsernameSlug(request.Username);
        var target = await userRepository.GetByUsernameSlugAsync(slug, cancellationToken);
        if (target is null)
            return UserErrors.NotFound;

        // Removes only the current user's own block row; the other side's block (if any) stands.
        // Idempotent: unblocking someone you haven't blocked is a no-op. Severed follows are NOT
        // restored — both sides start from a clean slate.
        await blockRepository.RemoveAsync(userId, target.Id, cancellationToken);
        return Result.Success;
    }
}
