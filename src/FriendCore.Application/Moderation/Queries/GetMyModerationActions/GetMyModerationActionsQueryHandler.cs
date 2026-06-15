using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Common;
using FriendCore.Domain.Moderation;
using MediatR;

namespace FriendCore.Application.Moderation.Queries.GetMyModerationActions;

internal sealed class GetMyModerationActionsQueryHandler(
    ICurrentUserService currentUserService,
    IAdminAuditLogRepository auditLogRepository)
    : IRequestHandler<GetMyModerationActionsQuery, ErrorOr<MyModerationActionsResponse>>
{
    private const int MaxResults = 100;

    public async Task<ErrorOr<MyModerationActionsResponse>> Handle(
        GetMyModerationActionsQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return CommonErrors.Unauthorized;

        // Account-level actions against this user (warnings/suspensions/bans). Content-removal
        // discovery (post-target) is a later addition — the user learns of those via the post.
        var actions = await auditLogRepository.GetForUserAsync(userId, MaxResults, cancellationToken);

        var items = actions
            .Select(action => new MyModerationActionResponse(
                action.Id.ToString(), action.Action.ToString(), action.Reason, action.CreatedAt))
            .ToList();

        return new MyModerationActionsResponse(items);
    }
}
