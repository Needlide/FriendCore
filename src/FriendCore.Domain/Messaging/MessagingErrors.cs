using ErrorOr;

namespace FriendCore.Domain.Messaging;

public static class MessagingErrors
{
    public static Error CannotMessageSelf =>
        Error.Validation("Messaging.CannotMessageSelf", "You cannot start a conversation with yourself.");

    // 404 (never 403): a conversation you are not part of — or one with a user you have a block
    // relationship with — simply does not exist for you (§8.2 anti-enumeration philosophy).
    public static Error ConversationNotFound =>
        Error.NotFound("Messaging.ConversationNotFound", "Conversation was not found.");

    // Same 404 treatment for the recipient: a blocked or non-existent user is indistinguishable.
    public static Error RecipientNotFound =>
        Error.NotFound("Messaging.RecipientNotFound", "User was not found.");

    // No admin account exists to staff the support inbox — a server misconfiguration, hence
    // Failure (→ 500), not NotFound. The client should show "support is unavailable, try later".
    public static Error SupportUnavailable =>
        Error.Failure("Messaging.SupportUnavailable", "Support chat is currently unavailable.");

    public static Error MessageEmpty =>
        Error.Validation("Messaging.MessageEmpty", "A message needs text or an attachment.");

    public static Error MessageTooLong =>
        Error.Validation("Messaging.MessageTooLong",
            $"Message must not exceed {Message.MaxContentLength} characters.");

    // Four matches a post's limit: the same visual grid, and the same client-side total-size
    // budget. Kept as a domain invariant so a future caller cannot invent a fifth slot.
    public static Error TooManyMedia =>
        Error.Validation("Messaging.TooManyMedia",
            $"A message can have at most {Message.MaxMediaPerMessage} images.");

    public static Error DuplicateMedia =>
        Error.Validation("Messaging.DuplicateMedia", "The same image cannot be attached twice.");

    // 404: the message is not in this conversation, or was soft-deleted (the global query
    // filter makes it invisible), so both cases are indistinguishable to the caller.
    /// <summary>
    ///     A search term too short to run. Its own code rather than an empty result, so the client
    ///     can say why nothing came back instead of showing a bare "no matches".
    /// </summary>
    public static Error SearchTermTooShort =>
        Error.Validation(
            "Messaging.SearchTermTooShort",
            "Type at least two characters to search.");

    public static Error MessageNotFound =>
        Error.NotFound("Messaging.MessageNotFound", "Message was not found.");

    public static Error NotMessageSender =>
        Error.Forbidden("Messaging.NotMessageSender", "You can only modify your own messages.");

    // An attachment-only message has no Content to rewrite. Rather than silently letting a
    // caption be added after the fact, the whole edit is refused.
    public static Error MessageNotEditable =>
        Error.Validation("Messaging.MessageNotEditable",
            "Only the text of a message can be edited.");

    // 404: the message being replied to is in another conversation, or was soft-deleted. Same
    // rule as editing a deleted message — replying to something that no longer exists would
    // leave a quote with nothing in it.
    public static Error ReplyTargetNotFound =>
        Error.NotFound("Messaging.ReplyTargetNotFound", "The message you are replying to is gone.");

    // Reactions come from a closed set (ReactionEmoji) — an unknown glyph is a client bug, not
// a free-text value to store. Validation, hence 400.
    public static Error ReactionNotAllowed =>
        Error.Validation("Messaging.ReactionNotAllowed", "That is not a supported reaction.");

    // 409, the only Conflict in the messaging module: the caller's view is out of date because
    // the sender saved a different wording from another device. Distinct from a validation
    // error — the text they sent may be perfectly fine, it is just no longer based on anything
    // current, so re-sending it unchanged would silently discard the other save.
    public static Error MessageChanged =>
        Error.Conflict("Messaging.MessageChanged",
            "This message was changed somewhere else. Reload it and try again.");
}
