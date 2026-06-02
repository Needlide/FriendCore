using FriendCore.Domain.Common;

namespace FriendCore.Domain.Users.Events;

public sealed record UserRegisteredEvent(Ulid UserId, string Email, DateTimeOffset OccurredAt) : IDomainEvent;