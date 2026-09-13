using FluentValidation;

namespace FriendCore.Application.Users.Commands.ResendVerificationEmail;

public sealed class ResendVerificationEmailCommandValidator : AbstractValidator<ResendVerificationEmailCommand>
{
    public ResendVerificationEmailCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithErrorCode("User.Email.Required")
            .WithMessage("Email is required.")
            .EmailAddress()
            .WithErrorCode("User.Email.InvalidFormat")
            .WithMessage("A valid email address is required.");
    }
}
