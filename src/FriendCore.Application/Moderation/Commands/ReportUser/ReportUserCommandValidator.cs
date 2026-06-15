using FluentValidation;
using FriendCore.Domain.Moderation;

namespace FriendCore.Application.Moderation.Commands.ReportUser;

public sealed class ReportUserCommandValidator : AbstractValidator<ReportUserCommand>
{
    public ReportUserCommandValidator()
    {
        RuleFor(command => command.Category)
            .IsInEnum()
            .WithErrorCode("Report.InvalidCategory");

        RuleFor(command => command.Note)
            .MaximumLength(Report.MaxNoteLength)
            .WithErrorCode("Report.NoteTooLong");
    }
}
