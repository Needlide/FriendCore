using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Domain.Common;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Users.Commands.BlockUser;

internal sealed class BlockUserCommandHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository,
    IBlockRepository blockRepository,
    IFollowRepository followRepository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<BlockUserCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(BlockUserCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return CommonErrors.Unauthorized;

        var slug = User.ToUsernameSlug(request.Username);
        var target = await userRepository.GetByUsernameSlugAsync(slug, cancellationToken);
        if (target is null || target.IsBanned)
            return UserErrors.NotFound;

        if (target.Id == userId)
            return UserErrors.CannotBlockSelf;

        // Idempotent: re-blocking is a no-op (any follows were already severed by the first block).
        if (await blockRepository.ExistsAsync(userId, target.Id, cancellationToken))
            return Result.Success;

        // Blocking severs follows in both directions, so when one exists the user must confirm.
        // The client never pre-checks follow state — this conflict is its signal to ask.
        if (!request.Confirmed && await followRepository.ExistsBetweenAsync(userId, target.Id, cancellationToken))
            return UserErrors.BlockRequiresConfirmation;

        await blockRepository.AddAndRemoveFollowsAsync(
            Block.Create(userId, target.Id, dateTimeProvider.UtcNow),
            cancellationToken);

        return Result.Success;
    }
}
