using FluentValidation;

namespace FriendCore.Application.Tags.Commands.ModerateTag;

public sealed class ModerateTagCommandValidator : AbstractValidator<ModerateTagCommand>
{
    public ModerateTagCommandValidator()
    {
        RuleFor(x => x.Action)
            .IsInEnum()
            .WithErrorCode("Tag.Moderation.InvalidAction")
            .WithMessage("Moderation action is invalid.");
    }
}
