using FluentValidation;
using FriendCore.Application.Common.Validation;

namespace FriendCore.Application.Users.Commands.ResetPassword;

public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty()
            .WithErrorCode("User.InvalidResetToken")
            .WithMessage("Reset token is required.");

        RuleFor(x => x.NewPassword).ApplyRegistrationPasswordRules();
    }
}
