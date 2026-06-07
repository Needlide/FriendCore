using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Common;
using FriendCore.Domain.Posts;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Posts.Commands.AddComment;

internal sealed class AddCommentCommandHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository,
    IPostRepository postRepository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<AddCommentCommand, ErrorOr<CommentResponse>>
{
    public async Task<ErrorOr<CommentResponse>> Handle(AddCommentCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return CommonErrors.Unauthorized;

        var author = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (author is null)
            return UserErrors.NotFound;

        var post = await postRepository.GetByIdAsync(request.PostId, cancellationToken);
        if (post is null)
            return PostErrors.NotFound;

        var commentResult = Comment.Create(post.Id, userId, request.Content, dateTimeProvider.UtcNow);
        if (commentResult.IsError)
            return commentResult.Errors;

        var comment = commentResult.Value;
        post.IncrementCommentCount();
        await postRepository.AddCommentAsync(comment, cancellationToken);

        return CommentResponse.Create(comment, author);
    }
}
