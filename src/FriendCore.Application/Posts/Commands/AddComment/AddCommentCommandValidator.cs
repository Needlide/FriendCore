using FluentValidation;
using FriendCore.Domain.Posts;

namespace FriendCore.Application.Posts.Commands.AddComment;

public sealed class AddCommentCommandValidator : AbstractValidator<AddCommentCommand>
{
    public AddCommentCommandValidator()
    {
        RuleFor(x => x.Content)
            .NotEmpty()
            .WithErrorCode("Post.CommentContentRequired")
            .WithMessage("Comment content is required.")
            .MaximumLength(Comment.MaxContentLength)
            .WithErrorCode("Post.CommentTooLong")
            .WithMessage($"Comment must not exceed {Comment.MaxContentLength} characters.");
    }
}
