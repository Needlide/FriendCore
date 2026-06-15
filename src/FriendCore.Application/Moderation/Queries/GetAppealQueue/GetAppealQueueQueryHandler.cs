using ErrorOr;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Moderation;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Moderation.Queries.GetAppealQueue;

internal sealed class GetAppealQueueQueryHandler(
    IAppealRepository appealRepository,
    IUserRepository userRepository)
    : IRequestHandler<GetAppealQueueQuery, ErrorOr<AppealQueueResponse>>
{
    private const int MaxResults = 100;

    public async Task<ErrorOr<AppealQueueResponse>> Handle(
        GetAppealQueueQuery request,
        CancellationToken cancellationToken)
    {
        var appeals = await appealRepository.GetQueueAsync(MaxResults, cancellationToken);
        if (appeals.Count == 0)
            return new AppealQueueResponse([]);

        var appellants = (await userRepository.GetByIdsIgnoringBlocksAsync(
                appeals.Select(appeal => appeal.AppellantId).Distinct().ToList(), cancellationToken))
            .ToDictionary(user => user.Id);

        var items = appeals
            .Where(appeal => appellants.ContainsKey(appeal.AppellantId))
            .Select(appeal => AppealResponse.Create(
                appeal, UserSummaryResponse.FromUser(appellants[appeal.AppellantId])))
            .ToList();

        return new AppealQueueResponse(items);
    }
}
