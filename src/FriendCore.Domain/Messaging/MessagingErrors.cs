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

    public static Error MessageContentRequired =>
        Error.Validation("Messaging.MessageContentRequired", "Message content is required.");

    public static Error MessageTooLong =>
        Error.Validation("Messaging.MessageTooLong",
            $"Message must not exceed {Message.MaxContentLength} characters.");
}
