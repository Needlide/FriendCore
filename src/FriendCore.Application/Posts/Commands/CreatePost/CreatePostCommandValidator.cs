using FluentValidation;
using FriendCore.Domain.Posts;

namespace FriendCore.Application.Posts.Commands.CreatePost;

public sealed class CreatePostCommandValidator : AbstractValidator<CreatePostCommand>
{
    public CreatePostCommandValidator()
    {
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.Content) || x.MediaIds.Count > 0)
            .WithErrorCode("Post.ContentOrMediaRequired")
            .WithMessage("A post needs text content or at least one media item.");

        RuleFor(x => x.Content)
            .MaximumLength(PostErrors.MaxContentLength)
            .WithErrorCode("Post.ContentTooLong")
            .WithMessage($"Post content must not exceed {PostErrors.MaxContentLength} characters.");

        RuleFor(x => x.Sensitivity)
            .IsInEnum()
            .WithErrorCode("Post.SensitivityInvalid")
            .WithMessage("Sensitivity is invalid.");

        RuleFor(x => x.MediaIds)
            .Must(ids => ids.Count <= PostErrors.MaxMediaPerPost)
            .WithErrorCode("Post.TooManyMedia")
            .WithMessage($"A post can have at most {PostErrors.MaxMediaPerPost} media items.");

        RuleForEach(x => x.MediaIds)
            .Must(id => Ulid.TryParse(id, out _))
            .WithErrorCode("Media.InvalidId")
            .WithMessage("Media id is not a valid identifier.");
    }
}
