using FluentValidation;

namespace FriendCore.Application.Users.Commands.UpdateProfile;

public sealed class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileCommandValidator()
    {
        RuleFor(x => x.DisplayName)
            .MaximumLength(100)
            .WithErrorCode("User.DisplayName.TooLong")
            .WithMessage("Display name must not exceed 100 characters.")
            .When(x => x.DisplayName is not null);

        RuleFor(x => x.Bio)
            .MaximumLength(500)
            .WithErrorCode("User.Bio.TooLong")
            .WithMessage("Bio must not exceed 500 characters.")
            .When(x => x.Bio is not null);

        RuleFor(x => x.WebsiteUrl)
            .MaximumLength(500)
            .WithErrorCode("User.WebsiteUrl.TooLong")
            .WithMessage("Website URL must not exceed 500 characters.")
            .When(x => x.WebsiteUrl is not null);

        RuleFor(x => x.Location)
            .MaximumLength(200)
            .WithErrorCode("User.Location.TooLong")
            .WithMessage("Location must not exceed 200 characters.")
            .When(x => x.Location is not null);

        RuleFor(x => x)
            .Must(command =>
                command.DisplayName is not null ||
                command.Bio is not null ||
                command.WebsiteUrl is not null ||
                command.Location is not null)
            .WithErrorCode("User.Profile.NoChanges")
            .WithMessage("At least one profile field must be provided.");
    }
}
