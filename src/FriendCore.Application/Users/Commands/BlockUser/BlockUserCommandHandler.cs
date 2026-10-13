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

        // Resolve across the block filter: keeps re-blocking idempotent, and lets a user place
        // their own block row even when the target blocked them first (protection that survives
        // the other side unblocking). Responses are identical either way, so nothing leaks.
        var slug = User.ToUsernameSlug(request.Username);
        var target = await userRepository.GetByUsernameSlugIgnoringBlocksAsync(slug, cancellationToken);
        if (target is null || target.IsBanned)
            return UserErrors.NotFound;

        if (target.Id == userId)
            return UserErrors.CannotBlockSelf;

        // The admin account staffs the support inbox — blocking it would sever the user's own
        // help channel, and an admin blocking a user would hide their support thread. Both
        // directions are rejected; moderation uses suspension/ban instead.
        if (target.Role == UserRole.Admin)
            return UserErrors.CannotBlockAdmin;

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
