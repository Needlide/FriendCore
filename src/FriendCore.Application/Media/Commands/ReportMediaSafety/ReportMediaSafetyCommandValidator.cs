using FluentValidation;

namespace FriendCore.Application.Media.Commands.ReportMediaSafety;

public sealed class ReportMediaSafetyCommandValidator : AbstractValidator<ReportMediaSafetyCommand>
{
    public ReportMediaSafetyCommandValidator()
    {
        RuleFor(x => x)
            .Must(x => x.Flashing || x.IntenseMotion)
            .WithErrorCode("Media.SafetyCategoryRequired")
            .WithMessage("At least one safety category must be reported.");
    }
}
