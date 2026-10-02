namespace FriendCore.Domain.Moderation;

/// <summary>
/// What a report points at. Stored as a string, so adding a member is not a schema change.
/// </summary>
public enum ReportTargetType
{
    Post,
    User,

    /// <summary>A single DM. Reportable without exposing the rest of the conversation.</summary>
    Message
}
