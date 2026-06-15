using ErrorOr;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Moderation;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Moderation.Queries.GetUserAccount;

internal sealed class GetUserAccountQueryHandler(
    IUserRepository userRepository,
    IAdminAuditLogRepository auditLogRepository)
    : IRequestHandler<GetUserAccountQuery, ErrorOr<UserAccountResponse>>
{
    private const int HistoryLimit = 100;

    public async Task<ErrorOr<UserAccountResponse>> Handle(
        GetUserAccountQuery request,
        CancellationToken cancellationToken)
    {
        // Block-ignoring: moderators inspect any account regardless of their own blocks.
        var slug = User.ToUsernameSlug(request.Username);
        var user = await userRepository.GetByUsernameSlugIgnoringBlocksAsync(slug, cancellationToken);
        if (user is null)
            return UserErrors.NotFound;

        var entries = await auditLogRepository.GetForUserAsync(user.Id, HistoryLimit, cancellationToken);
        var history = await BuildHistoryAsync(entries, cancellationToken);

        return UserAccountResponse.Create(user, history);
    }

    private async Task<IReadOnlyList<AuditLogEntryResponse>> BuildHistoryAsync(
        IReadOnlyList<AdminAuditLog> entries,
        CancellationToken cancellationToken)
    {
        if (entries.Count == 0)
            return [];

        var moderators = (await userRepository.GetByIdsIgnoringBlocksAsync(
                entries.Select(entry => entry.ModeratorId).Distinct().ToList(), cancellationToken))
            .ToDictionary(moderator => moderator.Id);

        return entries
            .Where(entry => moderators.ContainsKey(entry.ModeratorId))
            .Select(entry => AuditLogEntryResponse.Create(entry, UserSummaryResponse.FromUser(moderators[entry.ModeratorId])))
            .ToList();
    }
}
