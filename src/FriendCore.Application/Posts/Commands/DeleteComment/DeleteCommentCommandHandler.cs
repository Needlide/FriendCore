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

        // §5.3 MVP: the post author moderates their own thread, so either the comment author
        // or the author of the post the comment belongs to may delete it.
        var post = await postRepository.GetByIdAsync(comment.PostId, cancellationToken);
        if (comment.AuthorId != userId && post?.AuthorId != userId)
            return PostErrors.CommentNotAuthor;

        post?.DecrementCommentCount();

        await postRepository.SoftDeleteCommentAsync(comment, cancellationToken);
        return Result.Success;
    }
}
