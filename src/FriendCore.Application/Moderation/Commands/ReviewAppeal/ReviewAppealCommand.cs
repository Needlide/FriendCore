using ErrorOr;
using FriendCore.Application.Moderation.Common;
using MediatR;

namespace FriendCore.Application.Moderation.Commands.ReviewAppeal;

public sealed record ReviewAppealCommand(Ulid AppealId, AppealOutcomeInput Outcome, string Note)
    : IRequest<ErrorOr<Success>>;
