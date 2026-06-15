using FriendCore.Domain.Moderation;

namespace FriendCore.Application.Moderation.Common;

// Application-level mirror of the reviewable AppealStatus outcomes (Pending is not a choice).
public enum AppealOutcomeInput
{
    Upheld,
    PartiallyUpheld,
    Overturned
}

internal static class AppealOutcomeMapping
{
    public static AppealStatus ToDomain(this AppealOutcomeInput input) =>
        input switch
        {
            AppealOutcomeInput.Upheld => AppealStatus.Upheld,
            AppealOutcomeInput.PartiallyUpheld => AppealStatus.PartiallyUpheld,
            AppealOutcomeInput.Overturned => AppealStatus.Overturned,
            _ => throw new ArgumentOutOfRangeException(nameof(input), input, null)
        };
}
