using FluentValidation;
using FriendCore.Application.Moderation.Common;
using FriendCore.Domain.Moderation;

namespace FriendCore.Application.Moderation.Commands.ResolveReport;

public sealed class ResolveReportCommandValidator : AbstractValidator<ResolveReportCommand>
{
    public ResolveReportCommandValidator()
    {
        RuleFor(command => command.Action).IsInEnum().WithErrorCode("Moderation.InvalidAction");

        // Mandatory reason on every action (§10.3).
        RuleFor(command => command.Reason)
            .NotEmpty()
            .WithErrorCode("Moderation.ReasonRequired")
            .MaximumLength(AdminAuditLog.MaxReasonLength)
            .WithErrorCode("Moderation.ReasonTooLong");

        When(command => command.Action == ModerationActionInput.TemporarySuspension, () =>
            RuleFor(command => command.SuspensionDays)
                .NotNull()
                .InclusiveBetween(1, 365)
                .WithErrorCode("Moderation.InvalidSuspensionDuration"));
    }
}
