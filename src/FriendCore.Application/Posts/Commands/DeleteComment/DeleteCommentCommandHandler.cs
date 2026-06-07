using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Domain.Common;
using FriendCore.Domain.Posts;
using MediatR;

namespace FriendCore.Application.Posts.Commands.DeleteComment;

internal sealed class DeleteCommentCommandHandler(
    ICurrentUserService currentUserService,
    IPostRepository postRepository)
    : IRequestHandler<DeleteCommentCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(DeleteCommentCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return CommonErrors.Unauthorized;

        var comment = await postRepository.GetCommentByIdAsync(request.CommentId, cancellationToken);
        if (comment is null)
            return PostErrors.CommentNotFound;

        if (comment.AuthorId != userId)
            return PostErrors.CommentNotAuthor;

        var post = await postRepository.GetByIdAsync(comment.PostId, cancellationToken);
        post?.DecrementCommentCount();

        await postRepository.SoftDeleteCommentAsync(comment, cancellationToken);
        return Result.Success;
    }
}
