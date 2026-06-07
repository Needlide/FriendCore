using ErrorOr;
using FriendCore.Domain.Common;

namespace FriendCore.Domain.Tags;

public sealed class Tag : AggregateRoot<Ulid>
{
    private Tag(Ulid id, DateTimeOffset createdAt)
        : base(id, createdAt)
    {
    }

    private Tag()
    {
    }

    public string Name { get; private set; } = null!;
    public string Slug { get; private set; } = null!;
    public TagStatus Status { get; private set; }
    public Ulid CreatedById { get; private set; }
    public DateTimeOffset LastUsedAt { get; private set; }
    public int ProfileCount { get; private set; }
    public int PostCount { get; private set; }
    public int TotalCount { get; private set; }

    public bool IsAttachable => Status != TagStatus.Banned;

    public static Tag Create(string name, string slug, Ulid createdById, DateTimeOffset createdAt)
    {
        return new Tag(Ulid.NewUlid(), createdAt)
        {
            Name = name,
            Slug = slug,
            Status = TagStatus.PendingReview,
            CreatedById = createdById,
            LastUsedAt = createdAt
        };
    }

    public ErrorOr<Success> Approve()
    {
        if (Status == TagStatus.Banned)
            return TagErrors.Banned;

        Status = TagStatus.Active;
        return Result.Success;
    }

    public ErrorOr<Success> Archive()
    {
        if (Status == TagStatus.Banned)
            return TagErrors.Banned;

        Status = TagStatus.Archived;
        return Result.Success;
    }

    public void Ban()
    {
        Status = TagStatus.Banned;
    }

    public void RecordProfileAdded(DateTimeOffset at)
    {
        ProfileCount++;
        TotalCount = ProfileCount + PostCount;
        LastUsedAt = at;
    }

    public void RecordProfileRemoved()
    {
        if (ProfileCount > 0)
            ProfileCount--;

        TotalCount = ProfileCount + PostCount;
    }
}