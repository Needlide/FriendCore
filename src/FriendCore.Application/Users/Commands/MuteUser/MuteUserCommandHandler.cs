using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Domain.Common;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Users.Commands.MuteUser;

internal sealed class MuteUserCommandHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository,
    IMuteRepository muteRepository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<MuteUserCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(MuteUserCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return CommonErrors.Unauthorized;

        // Block-filtered resolution on purpose: across a block the target "does not exist" (404),
        // and muting would be meaningless anyway — blocks already hide everything.
        var slug = User.ToUsernameSlug(request.Username);
        var target = await userRepository.GetByUsernameSlugAsync(slug, cancellationToken);
        if (target is null || target.IsBanned)
            return UserErrors.NotFound;

        if (target.Id == userId)
            return UserErrors.CannotMuteSelf;

        // Idempotent: re-muting is a no-op. Muting does not touch follow relationships —
        // mute-but-stay-following is the primary use case.
        if (!await muteRepository.ExistsAsync(userId, target.Id, cancellationToken))
            await muteRepository.AddAsync(Mute.Create(userId, target.Id, dateTimeProvider.UtcNow), cancellationToken);

        return Result.Success;
    }
}
