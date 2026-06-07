using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Domain.Common;
using FriendCore.Domain.Posts;
using MediatR;

namespace FriendCore.Application.Posts.Commands.DeletePost;

internal sealed class DeletePostCommandHandler(
    ICurrentUserService currentUserService,
    IPostRepository postRepository)
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

        await postRepository.SoftDeleteAsync(post, cancellationToken);
        return Result.Success;
    }
}
