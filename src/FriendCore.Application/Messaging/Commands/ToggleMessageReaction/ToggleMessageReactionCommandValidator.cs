using FluentValidation;
using FriendCore.Domain.Messaging;

namespace FriendCore.Application.Messaging.Commands.ToggleMessageReaction;

public sealed class ToggleMessageReactionCommandValidator : AbstractValidator<ToggleMessageReactionCommand>
{
    public ToggleMessageReactionCommandValidator()
    {
        // Checked twice on purpose, exactly like SendMessage's content rules: here for the
        // request shape, and again in MessageReaction.Create for callers bypassing MediatR.
        // The allow-list is the real gate — a length rule alone would still let any glyph
        // through, which is the whole thing ReactionEmoji exists to prevent.
        RuleFor(command => command.Emoji)
            .Must(ReactionEmoji.IsAllowed)
            .WithErrorCode("Messaging.ReactionNotAllowed")
            .WithMessage("That is not a supported reaction.");
    }
}