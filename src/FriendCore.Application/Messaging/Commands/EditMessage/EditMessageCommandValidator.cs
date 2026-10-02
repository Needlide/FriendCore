using FluentValidation;
using FriendCore.Domain.Messaging;

namespace FriendCore.Application.Messaging.Commands.EditMessage;

public sealed class EditMessageCommandValidator : AbstractValidator<EditMessageCommand>
{
    public EditMessageCommandValidator()
    {
        // Enforced twice on purpose, exactly like SendMessage: here for the request shape, and
        // again in Message.Edit for callers that bypass MediatR.
        RuleFor(command => command.Content)
            .NotEmpty()
            .WithErrorCode("Messaging.MessageEmpty")
            .WithMessage("A message needs text or an attachment.")
            .MaximumLength(Message.MaxContentLength)
            .WithErrorCode("Messaging.MessageTooLong")
            .WithMessage($"Message must not exceed {Message.MaxContentLength} characters.");
    }
}
