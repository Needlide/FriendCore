using ErrorOr;

namespace FriendCore.Domain.Notifications;

public static class NotificationErrors
{
    // Another user's notification is indistinguishable from a missing one — never reveal that a
    // row exists to someone who does not own it.
    public static Error NotFound =>
        Error.NotFound("Notification.NotFound", "Notification was not found.");
}
