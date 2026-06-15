using FluentValidation;
using FriendCore.Domain.Moderation;

namespace FriendCore.Application.Moderation.Commands.ReportPost;

public sealed class ReportPostCommandValidator : AbstractValidator<ReportPostCommand>
{
    public ReportPostCommandValidator()
    {
        RuleFor(command => command.Category)
            .IsInEnum()
            .WithErrorCode("Report.InvalidCategory");

        RuleFor(command => command.Note)
            .MaximumLength(Report.MaxNoteLength)
            .WithErrorCode("Report.NoteTooLong");
    }
}
