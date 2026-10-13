namespace FriendCore.Domain.Messaging;

/// <summary>
///     Who removed a message, which decides whether the removal also records a consent refusal for
///     any image it carried.
/// </summary>
/// <remarks>
///     Passed explicitly rather than inferred because the answer is a judgement, not a fact: the
///     same code path deletes a message in three very different ways, and "does this mean the
///     recipient said no?" has a different answer each time.
/// </remarks>
public enum MessageDeletionSource
{
    /// <summary>
    ///     The sender withdrew it. No refusal is recorded — they declined to send it, which is not
    ///     anyone else's wish being overridden, and refusing it would stop them re-sharing the
    ///     same image later for no reason.
    /// </summary>
    Sender,

    /// <summary>
    ///     A moderator removed it. Refusal IS recorded: that is a judgement about the content
    ///     rather than a personal preference, so it should hold no matter who had the power to
    ///     delete it. Not produced yet — recipient-delete is still deferred, and this member is
    ///     the seam it plugs into.
    /// </summary>
    Moderation,

    /// <summary>The recipient declined it. The consent case this whole feature exists for.</summary>
    Recipient
}
