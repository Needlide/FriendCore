using FluentValidation;
using FriendCore.Domain.Messaging;

namespace FriendCore.Application.Messaging.Commands.SendMessage;

public sealed class SendMessageCommandValidator : AbstractValidator<SendMessageCommand>
{
    public SendMessageCommandValidator()
    {
        // "At least one of text or media" is enforced twice on purpose: here for the
        // request shape, and again in Message.Create for callers that bypass MediatR.
        RuleFor(command => command.Content)
            .Must((command, content) =>
                !string.IsNullOrWhiteSpace(content) || command.MediaIds is { Count: > 0 })
            .WithErrorCode("Messaging.MessageEmpty")
            .WithMessage("A message needs text or an attachment.")
            .DependentRules(() =>
            {
                RuleFor(command => command.Content)
                    .MaximumLength(Message.MaxContentLength)
                    .WithErrorCode("Messaging.MessageTooLong")
                    .WithMessage($"Message must not exceed {Message.MaxContentLength} characters.")
                    .When(command => command.Content is not null);
            });
    }
}
