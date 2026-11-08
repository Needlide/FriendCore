using FluentValidation;
using FriendCore.Domain.Posts;

namespace FriendCore.Application.Posts.Commands.UpdateComment;

public sealed class UpdateCommentCommandValidator : AbstractValidator<UpdateCommentCommand>
{
    public UpdateCommentCommandValidator()
    {
        RuleFor(x => x.Content)
            .NotEmpty()
            .WithErrorCode("Post.CommentContentRequired")
            .WithMessage("A comment needs text content.");

        RuleFor(x => x.Content)
            .MaximumLength(Comment.MaxContentLength)
            .When(x => x.Content is not null)
            .WithErrorCode("Post.CommentTooLong")
            .WithMessage($"Comment must not exceed {Comment.MaxContentLength} characters.");
    }
}
