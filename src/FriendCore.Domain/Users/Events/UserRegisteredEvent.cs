using FriendCore.Domain.Common;

namespace FriendCore.Domain.Users.Events;

public sealed record UserRegisteredEvent(
    Ulid UserId,
    string Email,
    string DisplayName,
    string Username,
    DateTimeOffset OccurredAt) : IDomainEvent;