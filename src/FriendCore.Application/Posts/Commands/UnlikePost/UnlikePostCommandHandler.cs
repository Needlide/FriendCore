using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Domain.Common;
using FriendCore.Domain.Posts;
using MediatR;

namespace FriendCore.Application.Posts.Commands.UnlikePost;

internal sealed class UnlikePostCommandHandler(
    ICurrentUserService currentUserService,
    IPostRepository postRepository)
    : IRequestHandler<UnlikePostCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(UnlikePostCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return CommonErrors.Unauthorized;

        var post = await postRepository.GetByIdAsync(request.PostId, cancellationToken);
        if (post is null)
            return PostErrors.NotFound;

        // Idempotent: unliking a post you haven't liked is a no-op.
        if (!await postRepository.LikeExistsAsync(post.Id, userId, cancellationToken))
            return Result.Success;

        post.DecrementLikeCount();
        await postRepository.RemoveLikeAsync(post.Id, userId, cancellationToken);
        return Result.Success;
    }
}
