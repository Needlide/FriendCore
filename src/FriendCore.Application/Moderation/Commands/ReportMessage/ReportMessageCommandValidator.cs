using FluentValidation;
using FriendCore.Domain.Moderation;

namespace FriendCore.Application.Moderation.Commands.ReportMessage;

public sealed class ReportMessageCommandValidator : AbstractValidator<ReportMessageCommand>
{
    public ReportMessageCommandValidator()
    {
        RuleFor(command => command.Category).IsInEnum().WithErrorCode("Report.InvalidCategory");
        RuleFor(command => command.Note).MaximumLength(Report.MaxNoteLength).WithErrorCode("Report.NoteTooLong");
    }
}
