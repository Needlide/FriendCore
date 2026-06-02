using FluentValidation;
using FriendCore.Application.Common.Validation;

namespace FriendCore.Application.Users.Commands.LoginUser;

public sealed class LoginUserCommandValidator : AbstractValidator<LoginUserCommand>
{
    public LoginUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithErrorCode("User.Email.Required")
            .WithMessage("Email is required.")
            .EmailAddress()
            .WithErrorCode("User.Email.InvalidFormat")
            .WithMessage("A valid email address is required.");

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithErrorCode("User.Password.Required")
            .WithMessage("Password is required.")
            .MaximumLength(PasswordValidationRules.MaxLength)
            .WithErrorCode(PasswordValidationRules.TooLongCode)
            .WithMessage($"Password must not exceed {PasswordValidationRules.MaxLength} characters.");
    }
}