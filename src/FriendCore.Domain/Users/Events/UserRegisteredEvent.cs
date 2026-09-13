using FriendCore.Domain.Common;

namespace FriendCore.Domain.Users.Events;

public sealed record UserRegisteredEvent(
    Ulid UserId,
    /// <summary>
    ///     Transient delivery address for the welcome/verification sends. In-memory only —
    ///     never persisted anywhere. Only the email hash reaches the database.
    /// </summary>
    string ContactEmail,
    string DisplayName,
    string Username,
    DateTimeOffset OccurredAt) : IDomainEvent;
