using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Domain.Common;
using FriendCore.Domain.Posts;
using MediatR;

namespace FriendCore.Application.Posts.Commands.LikePost;

internal sealed class LikePostCommandHandler(
    ICurrentUserService currentUserService,
    IPostRepository postRepository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<LikePostCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(LikePostCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return CommonErrors.Unauthorized;

        var post = await postRepository.GetByIdAsync(request.PostId, cancellationToken);
        if (post is null)
            return PostErrors.NotFound;

        // Idempotent: liking an already-liked post is a no-op.
        if (await postRepository.LikeExistsAsync(post.Id, userId, cancellationToken))
            return Result.Success;

        post.IncrementLikeCount();
        await postRepository.AddLikeAsync(Like.Create(post.Id, userId, dateTimeProvider.UtcNow), cancellationToken);
        return Result.Success;
    }
}
