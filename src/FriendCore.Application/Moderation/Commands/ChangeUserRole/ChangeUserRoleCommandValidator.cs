using FluentValidation;
using FriendCore.Domain.Moderation;

namespace FriendCore.Application.Moderation.Commands.ChangeUserRole;

public sealed class ChangeUserRoleCommandValidator : AbstractValidator<ChangeUserRoleCommand>
{
    public ChangeUserRoleCommandValidator()
    {
        RuleFor(command => command.Role).IsInEnum().WithErrorCode("Moderation.InvalidRole");

        RuleFor(command => command.Reason)
            .NotEmpty()
            .WithErrorCode("Moderation.ReasonRequired")
            .MaximumLength(AdminAuditLog.MaxReasonLength)
            .WithErrorCode("Moderation.ReasonTooLong");
    }
}
