namespace FriendCore.Domain.Notifications;

public enum NotificationType
{
    NewFollower,
    NewLike,
    NewComment,
    NewMessage,

    // §10/§13: people learn the outcome of their own report, appeal, or enforcement action.
    ReportResolved,
    AppealResolved,
    ModerationAction
}
