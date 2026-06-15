using FluentValidation;
using FriendCore.Domain.Moderation;

namespace FriendCore.Application.Moderation.Commands.ReviewAppeal;

public sealed class ReviewAppealCommandValidator : AbstractValidator<ReviewAppealCommand>
{
    public ReviewAppealCommandValidator()
    {
        RuleFor(command => command.Outcome).IsInEnum().WithErrorCode("Appeal.InvalidOutcome");

        // §13.5 mandatory notes on moderator decisions.
        RuleFor(command => command.Note)
            .NotEmpty()
            .WithErrorCode("Appeal.ReviewNoteRequired")
            .MaximumLength(Appeal.MaxReviewNoteLength)
            .WithErrorCode("Appeal.ReviewNoteTooLong");
    }
}
