using MediatR;

namespace FriendCore.Application.Notifications.Events;

// Application events published by command handlers after a successful write, consumed by the
// notification event handlers. Plain MediatR notifications — not aggregate domain events —
// because the triggering entities (Follow/Like/Comment/Message) are deliberately non-aggregates.

public sealed record UserFollowedEvent(Ulid FollowedId, Ulid FollowerId, DateTimeOffset OccurredAt) : INotification;

public sealed record PostLikedEvent(Ulid PostAuthorId, Ulid ActorId, Ulid PostId, DateTimeOffset OccurredAt)
    : INotification;

public sealed record PostCommentedEvent(Ulid PostAuthorId, Ulid ActorId, Ulid PostId, DateTimeOffset OccurredAt)
    : INotification;

public sealed record MessageSentEvent(Ulid RecipientId, Ulid SenderId, Ulid ConversationId, DateTimeOffset OccurredAt)
    : INotification;
