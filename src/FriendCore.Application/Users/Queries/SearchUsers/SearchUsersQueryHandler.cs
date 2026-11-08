using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Users.Queries.SearchUsers;

internal sealed class SearchUsersQueryHandler(
    IUserRepository userRepository,
    ICurrentUserService currentUserService)
    : IRequestHandler<SearchUsersQuery, ErrorOr<UserSearchResponse>>
{
    private const int MinQueryLength = 2;
    private const int MaxResults = 20;

    public async Task<ErrorOr<UserSearchResponse>> Handle(
        SearchUsersQuery request,
        CancellationToken cancellationToken)
    {
        var query = request.Query?.Trim() ?? string.Empty;

        // A single character matches almost everyone and is expensive; the client also waits
        // for two characters. Return an empty result rather than an error.
        if (query.Length < MinQueryLength)
            return new UserSearchResponse([]);

        var users = await userRepository.SearchAsync(query, MaxResults, cancellationToken);

        // The repository already excludes banned, soft-deleted, and block-hidden accounts.
        var viewerId = currentUserService.UserId;
        var results = users
            .Where(user => user.Id != viewerId)
            .Select(user => UserSummaryResponse.FromUser(user))
            .ToList();

        return new UserSearchResponse(results);
    }
}
