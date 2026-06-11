using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Application.Posts.Common;
using FriendCore.Domain.Posts;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Posts.Queries.GetPost;

internal sealed class GetPostQueryHandler(
    ICurrentUserService currentUserService,
    IPostRepository postRepository,
    IUserRepository userRepository,
    PostMediaResponseFactory mediaResponseFactory)
    : IRequestHandler<GetPostQuery, ErrorOr<PostResponse>>
{
    public async Task<ErrorOr<PostResponse>> Handle(GetPostQuery request, CancellationToken cancellationToken)
    {
        var post = await postRepository.GetByIdAsync(request.PostId, cancellationToken);
        if (post is null)
            return PostErrors.NotFound;

        var author = await userRepository.GetByIdAsync(post.AuthorId, cancellationToken);
        if (author is null)
            return PostErrors.NotFound;

        var isLiked = currentUserService.UserId is { } userId &&
                      (await postRepository.GetLikedPostIdsAsync(userId, [post.Id], cancellationToken)).Count > 0;

        var mediaByPost = await mediaResponseFactory.ForPostsAsync([post], cancellationToken);
        return PostResponse.Create(post, author, isLiked, mediaByPost.GetValueOrDefault(post.Id, []));
    }
}
