using ErrorOr;
using FriendCore.Application.Moderation.Common;
using FriendCore.Application.Moderation.Services;
using FriendCore.Domain.Common;
using FriendCore.Domain.Moderation;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Moderation.Commands.ActOnUser;

internal sealed class ActOnUserCommandHandler(
    IUserRepository userRepository,
    IModerationService moderationService)
    : IRequestHandler<ActOnUserCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(ActOnUserCommand request, CancellationToken cancellationToken)
    {
        var slug = User.ToUsernameSlug(request.Username);
        var target = await userRepository.GetByUsernameSlugIgnoringBlocksAsync(slug, cancellationToken);
        if (target is null)
            return UserErrors.NotFound;

        var action = request.Action.ToDomain();

        // Content removal and report-dismissal are not user-directed actions (the validator also
        // blocks them, but guard here so the service contract stays honest).
        if (action is ModerationActionType.ContentRemoval or ModerationActionType.ReportDismissed)
            return ModerationErrors.ActionRequiresUser;

        return await moderationService.ExecuteAsync(
            action,
            ReportTargetType.User,
            target.Id,
            request.Reason,
            request.SuspensionDays,
            report: null,
            cancellationToken);
    }
}
