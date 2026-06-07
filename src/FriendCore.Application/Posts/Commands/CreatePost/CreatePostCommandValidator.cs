using FluentValidation;
using FriendCore.Domain.Posts;

namespace FriendCore.Application.Posts.Commands.CreatePost;

public sealed class CreatePostCommandValidator : AbstractValidator<CreatePostCommand>
{
    public CreatePostCommandValidator()
    {
        RuleFor(x => x.Content)
            .NotEmpty()
            .WithErrorCode("Post.ContentRequired")
            .WithMessage("Post content is required.")
            .MaximumLength(PostErrors.MaxContentLength)
            .WithErrorCode("Post.ContentTooLong")
            .WithMessage($"Post content must not exceed {PostErrors.MaxContentLength} characters.");

        RuleFor(x => x.Sensitivity)
            .IsInEnum()
            .WithErrorCode("Post.SensitivityInvalid")
            .WithMessage("Sensitivity is invalid.");
    }
}
