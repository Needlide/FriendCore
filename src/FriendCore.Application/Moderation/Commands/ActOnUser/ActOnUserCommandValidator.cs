using FluentValidation;
using FriendCore.Application.Moderation.Common;
using FriendCore.Domain.Moderation;

namespace FriendCore.Application.Moderation.Commands.ActOnUser;

public sealed class ActOnUserCommandValidator : AbstractValidator<ActOnUserCommand>
{
    private static readonly ModerationActionInput[] UserActions =
    [
        ModerationActionInput.Warning,
        ModerationActionInput.TemporaryRestriction,
        ModerationActionInput.TemporarySuspension,
        ModerationActionInput.PermanentBan,
        ModerationActionInput.LegalReferral
    ];

    public ActOnUserCommandValidator()
    {
        RuleFor(command => command.Action)
            .Must(action => UserActions.Contains(action))
            .WithErrorCode("Moderation.ActionRequiresUser")
            .WithMessage("This action cannot be applied directly to a user.");

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
