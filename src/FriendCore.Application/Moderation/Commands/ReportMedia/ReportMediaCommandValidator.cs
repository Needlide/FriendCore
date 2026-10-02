using FluentValidation;
using FriendCore.Domain.Moderation;

namespace FriendCore.Application.Moderation.Commands.ReportMedia;

public sealed class ReportMediaCommandValidator : AbstractValidator<ReportMediaCommand>
{
    public ReportMediaCommandValidator()
    {
        RuleFor(command => command.Category).IsInEnum().WithErrorCode("Report.InvalidCategory");
        RuleFor(command => command.Note).MaximumLength(Report.MaxNoteLength).WithErrorCode("Report.NoteTooLong");
    }
}
