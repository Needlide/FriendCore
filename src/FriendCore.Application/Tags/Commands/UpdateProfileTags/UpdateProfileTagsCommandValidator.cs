using FluentValidation;
using FriendCore.Domain.Tags;

namespace FriendCore.Application.Tags.Commands.UpdateProfileTags;

public sealed class UpdateProfileTagsCommandValidator : AbstractValidator<UpdateProfileTagsCommand>
{
    public UpdateProfileTagsCommandValidator()
    {
        RuleFor(x => x.Tags)
            .NotNull()
            .WithErrorCode("Tag.Profile.Required")
            .WithMessage("Tags collection is required.");

        RuleForEach(x => x.Tags).ChildRules(tag =>
        {
            tag.RuleFor(t => t.Name)
                .NotEmpty()
                .WithErrorCode("Tag.Name.Required")
                .WithMessage("Tag name is required.")
                .MaximumLength(100)
                .WithErrorCode("Tag.Name.TooLong")
                .WithMessage("Tag name must not exceed 100 characters.");

            tag.RuleFor(t => t.Tier)
                .IsInEnum()
                .WithErrorCode("Tag.Tier.Invalid")
                .WithMessage("Tag tier is invalid.");
        });
    }
}
