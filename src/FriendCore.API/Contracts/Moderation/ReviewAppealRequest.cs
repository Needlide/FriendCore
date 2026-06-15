using FriendCore.Application.Moderation.Common;

namespace FriendCore.API.Contracts.Moderation;

public sealed record ReviewAppealRequest(AppealOutcomeInput Outcome, string Note);
