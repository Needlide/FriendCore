using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Notifications.Events;
using FriendCore.Domain.Common;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Users.Commands.FollowUser;

internal sealed class FollowUserCommandHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository,
    IFollowRepository followRepository,
    IBlockRepository blockRepository,
    IPublisher publisher,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<FollowUserCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(FollowUserCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return CommonErrors.Unauthorized;

        var slug = User.ToUsernameSlug(request.Username);
        var target = await userRepository.GetByUsernameSlugAsync(slug, cancellationToken);
        if (target is null || target.IsBanned)
            return UserErrors.NotFound;

        if (target.Id == userId)
            return UserErrors.CannotFollowSelf;

        // §8.2: a block in either direction makes the target invisible — 404, never 403,
        // so a blocked user cannot learn they were blocked.
        if (await blockRepository.ExistsBetweenAsync(userId, target.Id, cancellationToken))
            return UserErrors.NotFound;

        // Idempotent: re-following is a no-op — and must not re-fire a notification.
        if (!await followRepository.ExistsAsync(userId, target.Id, cancellationToken))
        {
            var now = dateTimeProvider.UtcNow;
            await followRepository.AddAsync(Follow.Create(userId, target.Id, now), cancellationToken);
            await publisher.Publish(new UserFollowedEvent(target.Id, userId, now), cancellationToken);
        }

        return Result.Success;
    }
}
