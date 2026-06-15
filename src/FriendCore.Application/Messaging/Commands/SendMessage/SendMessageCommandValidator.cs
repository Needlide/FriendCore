using FluentValidation;
using FriendCore.Domain.Messaging;

namespace FriendCore.Application.Messaging.Commands.SendMessage;

public sealed class SendMessageCommandValidator : AbstractValidator<SendMessageCommand>
{
    public SendMessageCommandValidator()
    {
        RuleFor(command => command.Content)
            .NotEmpty()
            .WithErrorCode("Messaging.MessageContentRequired")
            .WithMessage("Message content is required.")
            .MaximumLength(Message.MaxContentLength)
            .WithErrorCode("Messaging.MessageTooLong")
            .WithMessage($"Message must not exceed {Message.MaxContentLength} characters.");
    }
}
