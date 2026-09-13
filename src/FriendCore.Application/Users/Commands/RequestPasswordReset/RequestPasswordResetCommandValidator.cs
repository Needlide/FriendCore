using FluentValidation;

namespace FriendCore.Application.Users.Commands.RequestPasswordReset;

public sealed class RequestPasswordResetCommandValidator : AbstractValidator<RequestPasswordResetCommand>
{
    public RequestPasswordResetCommandValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty()
            .WithErrorCode("User.Username.Required")
            .WithMessage("Username is required.");

        RuleFor(x => x.Email)
            .NotEmpty()
            .WithErrorCode("User.Email.Required")
            .WithMessage("Email is required.")
            .EmailAddress()
            .WithErrorCode("User.Email.InvalidFormat")
            .WithMessage("A valid email address is required.");
    }
}
