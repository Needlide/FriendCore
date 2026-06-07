using FluentValidation;

namespace FriendCore.Application.Tags.Commands.CreateOrGetTag;

public sealed class CreateOrGetTagCommandValidator : AbstractValidator<CreateOrGetTagCommand>
{
    public CreateOrGetTagCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithErrorCode("Tag.Name.Required")
            .WithMessage("Tag name is required.")
            .MaximumLength(100)
            .WithErrorCode("Tag.Name.TooLong")
            .WithMessage("Tag name must not exceed 100 characters.");
    }
}
