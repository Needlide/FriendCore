using FluentValidation;
using FriendCore.Domain.Moderation;

namespace FriendCore.Application.Moderation.Commands.SubmitAppeal;

public sealed class SubmitAppealCommandValidator : AbstractValidator<SubmitAppealCommand>
{
    public SubmitAppealCommandValidator()
    {
        RuleFor(command => command.Reason)
            .NotEmpty()
            .WithErrorCode("Appeal.ReasonRequired")
            .MaximumLength(Appeal.MaxReasonLength)
            .WithErrorCode("Appeal.ReasonTooLong");
    }
}
