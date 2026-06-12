using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Application.Posts.Common;
using FriendCore.Domain.Common;
using FriendCore.Domain.Posts;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Posts.Queries.GetHomeFeed;

internal sealed class GetHomeFeedQueryHandler(
    ICurrentUserService currentUserService,
    IPostRepository postRepository,
    IUserRepository userRepository,
    PostResponseAssembler responseAssembler)
    : IRequestHandler<GetHomeFeedQuery, ErrorOr<PostFeedResponse>>
{
    private const int PageSize = 20;

    public async Task<ErrorOr<PostFeedResponse>> Handle(GetHomeFeedQuery request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return CommonErrors.Unauthorized;

        var cursor = PostCursor.Decode(request.Cursor);
        var viewer = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (viewer is null)
            return CommonErrors.Unauthorized;

        var preferences = await userRepository.GetPreferencesAsync(userId, cancellationToken);
        var keywordFilters = preferences?.KeywordFilters ?? [];
        var posts = await postRepository.GetHomeFeedAsync(
            userId, keywordFilters, viewer.ContentPreference, cursor, PageSize + 1, cancellationToken);

        var hasMore = posts.Count > PageSize;
        var page = hasMore ? posts.Take(PageSize).ToList() : posts;

        var items = await responseAssembler.AssembleAsync(page, userId, cancellationToken);

        var nextCursor = hasMore ? PostCursor.Encode(page[^1].CreatedAt, page[^1].Id) : null;
        return new PostFeedResponse(items, nextCursor);
    }
}