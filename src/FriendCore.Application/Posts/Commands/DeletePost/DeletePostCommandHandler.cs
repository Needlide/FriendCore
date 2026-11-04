using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Domain.Common;
using FriendCore.Domain.Posts;
using FriendCore.Domain.Tags;
using MediatR;

namespace FriendCore.Application.Posts.Commands.DeletePost;

internal sealed class DeletePostCommandHandler(
    ICurrentUserService currentUserService,
    IPostRepository postRepository,
    ITagRepository tagRepository)
    : IRequestHandler<DeletePostCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(DeletePostCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return CommonErrors.Unauthorized;

        var post = await postRepository.GetByIdAsync(request.PostId, cancellationToken);
        if (post is null)
            return PostErrors.NotFound;

        if (post.AuthorId != userId)
            return PostErrors.NotAuthor;

        // Usage counts come back down with the post; persisted by SoftDeleteAsync's SaveChanges
        // over the shared context.
        if (post.Tags.Count > 0)
        {
            var tags = await tagRepository.GetByIdsAsync(
                post.Tags.Select(tag => tag.TagId).ToList(), cancellationToken);
            foreach (var tag in tags)
                tag.RecordPostRemoved();
        }

        // The post's comments and likes die with it: staged, not saved — SoftDeleteAsync's
        // SaveChanges commits everything in one unit of work.
        await postRepository.StageSoftDeleteCommentsForPostAsync(post.Id, cancellationToken);
        await postRepository.StageRemoveLikesForPostAsync(post.Id, cancellationToken);

        await postRepository.SoftDeleteAsync(post, cancellationToken);
        return Result.Success;
    }
}
