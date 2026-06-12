namespace FriendCore.Domain.Users;

/// <summary>
///     One-way (§13.6): hides the muted user's content from the muter's feed/discovery/search
///     only. The muted user is unaffected and unaware; direct profile and post access stay open.
///     Deliberately NOT enforced by a global query filter — applied per query instead.
/// </summary>
public sealed class Mute
{
    private Mute()
    {
    }

    public Ulid MuterId { get; private set; }
    public Ulid MutedId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static Mute Create(Ulid muterId, Ulid mutedId, DateTimeOffset createdAt)
    {
        return new Mute
        {
            MuterId = muterId,
            MutedId = mutedId,
            CreatedAt = createdAt
        };
    }
}
