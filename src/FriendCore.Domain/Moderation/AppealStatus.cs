namespace FriendCore.Domain.Moderation;

// §13.3 outcomes. PartiallyUpheld = the action had partial merit (recorded; any concrete
// reduction is applied via a normal moderation action). Overturned = reverse the enforcement.
public enum AppealStatus
{
    Pending,
    Upheld,
    PartiallyUpheld,
    Overturned
}
