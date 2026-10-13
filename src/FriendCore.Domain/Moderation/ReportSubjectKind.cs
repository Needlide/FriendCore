namespace FriendCore.Domain.Moderation;

/// <summary>
///     What the captured snapshot actually holds, so the moderation queue can render it honestly
///     instead of guessing from the content.
/// </summary>
/// <remarks>
///     Stored as a string: adding a member must not be a schema change, same as the other enums on
///     <see cref="Report" />.
/// </remarks>
public enum ReportSubjectKind
{
    /// <summary>Body text of a post or a message.</summary>
    Text,

    /// <summary>A user's name, captured because usernames change and the report outlives them.</summary>
    Username,

    /// <summary>
    ///     An uploaded file. Deliberately only the identifying details — the bytes are NOT copied
    ///     into the report. Media is soft-deleted, so the original is still retrievable from object
    ///     storage; duplicating it here would be both wasteful and a second copy of the very
    ///     content the user asked to be taken down.
    /// </summary>
    Image
}
