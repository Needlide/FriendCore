namespace FriendCore.Domain.Tags;

public sealed class ProfileTag
{
    private ProfileTag()
    {
    }

    public Ulid UserId { get; private set; }
    public Ulid TagId { get; private set; }
    public TagTier Tier { get; private set; }
    public bool IsPublic { get; private set; }
    public DateTimeOffset AddedAt { get; private set; }

    internal static ProfileTag Create(Ulid userId, Ulid tagId, TagTier tier, bool isPublic, DateTimeOffset addedAt)
    {
        return new ProfileTag
        {
            UserId = userId,
            TagId = tagId,
            Tier = tier,
            IsPublic = isPublic,
            AddedAt = addedAt
        };
    }
}
