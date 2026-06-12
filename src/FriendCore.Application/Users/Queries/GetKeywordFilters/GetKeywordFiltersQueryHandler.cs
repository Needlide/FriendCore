using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Common;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Users.Queries.GetKeywordFilters;

internal sealed class GetKeywordFiltersQueryHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository)
    : IRequestHandler<GetKeywordFiltersQuery, ErrorOr<KeywordFiltersResponse>>
{
    public async Task<ErrorOr<KeywordFiltersResponse>> Handle(
        GetKeywordFiltersQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return CommonErrors.Unauthorized;

        var preferences = await userRepository.GetPreferencesAsync(userId, cancellationToken);
        if (preferences is null)
            return UserErrors.NotFound;

        return new KeywordFiltersResponse(preferences.KeywordFilters);
    }
}
