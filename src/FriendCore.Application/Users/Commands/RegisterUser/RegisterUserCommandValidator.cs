using FluentValidation;
using FriendCore.Application.Common.Validation;

namespace FriendCore.Application.Users.Commands.RegisterUser;

public sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty()
            .WithErrorCode("User.Username.Required")
            .WithMessage("Username is required.")
            .MinimumLength(3)
            .WithErrorCode("User.Username.TooShort")
            .WithMessage("Username must be at least 3 characters.")
            .MaximumLength(30)
            .WithErrorCode("User.Username.TooLong")
            .WithMessage("Username must not exceed 30 characters.")
            .Matches("^[a-zA-Z0-9_]+$")
            .WithErrorCode("User.Username.InvalidFormat")
            .WithMessage("Username must contain only letters, numbers, or underscores.");

        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .WithErrorCode("User.DisplayName.Required")
            .WithMessage("Display name is required.")
            .MaximumLength(100)
            .WithErrorCode("User.DisplayName.TooLong")
            .WithMessage("Display name must not exceed 100 characters.");

        RuleFor(x => x.Email)
            .NotEmpty()
            .WithErrorCode("User.Email.Required")
            .WithMessage("Email is required.")
            .EmailAddress()
            .WithErrorCode("User.Email.InvalidFormat")
            .WithMessage("A valid email address is required.")
            .MaximumLength(320)
            .WithErrorCode("User.Email.TooLong")
            .WithMessage("Email must not exceed 320 characters.");

        RuleFor(x => x.Password).ApplyRegistrationPasswordRules();
    }
}