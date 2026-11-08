using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Common;
using FriendCore.Domain.Posts;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Posts.Commands.UpdateComment;

internal sealed class UpdateCommentCommandHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository,
    IPostRepository postRepository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<UpdateCommentCommand, ErrorOr<CommentResponse>>
{
    public async Task<ErrorOr<CommentResponse>> Handle(UpdateCommentCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return CommonErrors.Unauthorized;

        var comment = await postRepository.GetCommentByIdAsync(request.CommentId, cancellationToken);
        if (comment is null || comment.AuthorId != userId)
            return comment is null ? PostErrors.CommentNotFound : PostErrors.CommentNotAuthor;

        var edit = comment.Edit(request.Content, dateTimeProvider.UtcNow);
        if (edit.IsError)
            return edit.Errors;

        await postRepository.UpdateCommentAsync(comment, cancellationToken);

        var author = await userRepository.GetByIdAsync(comment.AuthorId, cancellationToken);
        return author is null ? UserErrors.NotFound : CommentResponse.Create(comment, author);
    }
}
