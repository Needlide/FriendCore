using FluentValidation;

namespace FriendCore.Application.Users.Commands.VerifyEmail;

public sealed class VerifyEmailCommandValidator : AbstractValidator<VerifyEmailCommand>
{
    public VerifyEmailCommandValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty()
            .WithErrorCode("User.VerificationToken.Required")
            .WithMessage("Verification token is required.")
            .MaximumLength(256)
            .WithErrorCode("User.VerificationToken.TooLong")
            .WithMessage("Verification token is invalid.");
    }
}