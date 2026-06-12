using FluentValidation;

namespace FriendCore.Application.Users.Commands.UpdatePreferences;

internal sealed class UpdatePreferencesCommandValidator : AbstractValidator<UpdatePreferencesCommand>
{
    public UpdatePreferencesCommandValidator()
    {
        RuleFor(command => command.ContentPreference).IsInEnum();
    }
}
