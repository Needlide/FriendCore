using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Domain.Common;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Users.Commands.FollowUser;

internal sealed class FollowUserCommandHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository,
    IFollowRepository followRepository,
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

        // Idempotent: re-following is a no-op.
        if (!await followRepository.ExistsAsync(userId, target.Id, cancellationToken))
            await followRepository.AddAsync(Follow.Create(userId, target.Id, dateTimeProvider.UtcNow), cancellationToken);

        return Result.Success;
    }
}
