using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Common;
using FriendCore.Domain.Posts;
using MediatR;

namespace FriendCore.Application.Posts.Commands.UnlikePost;

internal sealed class UnlikePostCommandHandler(
    ICurrentUserService currentUserService,
    IPostRepository postRepository)
    : IRequestHandler<UnlikePostCommand, ErrorOr<PostLikeResponse>>
{
    public async Task<ErrorOr<PostLikeResponse>> Handle(UnlikePostCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return CommonErrors.Unauthorized;

        var post = await postRepository.GetByIdAsync(request.PostId, cancellationToken);
        if (post is null)
            return PostErrors.NotFound;

        // Idempotent: unliking a post you haven't liked is a no-op but still returns current state.
        if (!await postRepository.LikeExistsAsync(post.Id, userId, cancellationToken))
            return new PostLikeResponse(false, post.LikeCount);

        post.DecrementLikeCount();
        await postRepository.RemoveLikeAsync(post.Id, userId, cancellationToken);
        return new PostLikeResponse(false, post.LikeCount);
    }
}
