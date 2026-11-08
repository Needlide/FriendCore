using FluentValidation;
using FriendCore.Domain.Posts;

namespace FriendCore.Application.Posts.Commands.UpdatePost;

public sealed class UpdatePostCommandValidator : AbstractValidator<UpdatePostCommand>
{
    public UpdatePostCommandValidator()
    {
        // The content-or-media rule needs the stored media count, so the handler enforces it.
        RuleFor(x => x.Content)
            .MaximumLength(PostErrors.MaxContentLength)
            .WithErrorCode("Post.ContentTooLong")
            .WithMessage($"Post content must not exceed {PostErrors.MaxContentLength} characters.");

        RuleFor(x => x.Sensitivity)
            .IsInEnum()
            .WithErrorCode("Post.SensitivityInvalid")
            .WithMessage("Sensitivity is invalid.");
    }
}
