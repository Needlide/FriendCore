using ErrorOr;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Moderation;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Moderation.Queries.GetAuditLog;

internal sealed class GetAuditLogQueryHandler(
    IAdminAuditLogRepository auditLogRepository,
    IUserRepository userRepository)
    : IRequestHandler<GetAuditLogQuery, ErrorOr<AuditLogResponse>>
{
    private const int MaxResults = 100;

    public async Task<ErrorOr<AuditLogResponse>> Handle(GetAuditLogQuery request, CancellationToken cancellationToken)
    {
        var entries = await auditLogRepository.GetRecentAsync(MaxResults, cancellationToken);
        if (entries.Count == 0)
            return new AuditLogResponse([]);

        var moderators = (await userRepository.GetByIdsIgnoringBlocksAsync(
                entries.Select(entry => entry.ModeratorId).Distinct().ToList(), cancellationToken))
            .ToDictionary(moderator => moderator.Id);

        var items = entries
            .Where(entry => moderators.ContainsKey(entry.ModeratorId))
            .Select(entry => AuditLogEntryResponse.Create(
                entry, UserSummaryResponse.FromUser(moderators[entry.ModeratorId])))
            .ToList();

        return new AuditLogResponse(items);
    }
}
