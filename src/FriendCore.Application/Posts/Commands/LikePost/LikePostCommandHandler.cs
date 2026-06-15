using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Application.Notifications.Events;
using FriendCore.Domain.Common;
using FriendCore.Domain.Posts;
using MediatR;

namespace FriendCore.Application.Posts.Commands.LikePost;

internal sealed class LikePostCommandHandler(
    ICurrentUserService currentUserService,
    IPostRepository postRepository,
    IPublisher publisher,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<LikePostCommand, ErrorOr<PostLikeResponse>>
{
    public async Task<ErrorOr<PostLikeResponse>> Handle(LikePostCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return CommonErrors.Unauthorized;

        var post = await postRepository.GetByIdAsync(request.PostId, cancellationToken);
        if (post is null)
            return PostErrors.NotFound;

        // Idempotent: liking an already-liked post is a no-op but still returns current state —
        // and must not re-fire a notification.
        if (await postRepository.LikeExistsAsync(post.Id, userId, cancellationToken))
            return new PostLikeResponse(true, post.LikeCount);

        var now = dateTimeProvider.UtcNow;
        post.IncrementLikeCount();
        await postRepository.AddLikeAsync(Like.Create(post.Id, userId, now), cancellationToken);
        await publisher.Publish(new PostLikedEvent(post.AuthorId, userId, post.Id, now), cancellationToken);
        return new PostLikeResponse(true, post.LikeCount);
    }
}
