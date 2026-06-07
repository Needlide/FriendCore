using FriendCore.Domain.Tags;

namespace FriendCore.UnitTests.Tags;

public class TagTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 2, 9, 0, 0, TimeSpan.Zero);

    private static Tag NewTag() => Tag.Create("C Sharp", "c-sharp", Ulid.NewUlid(), Now);

    [Fact]
    public void Create_StartsPendingReview_WithZeroCounts()
    {
        var tag = NewTag();

        Assert.Equal(TagStatus.PendingReview, tag.Status);
        Assert.Equal("C Sharp", tag.Name);
        Assert.Equal("c-sharp", tag.Slug);
        Assert.Equal(0, tag.TotalCount);
        Assert.Equal(Now, tag.LastUsedAt);
        Assert.True(tag.IsAttachable);
    }

    [Fact]
    public void Approve_TransitionsToActive()
    {
        var tag = NewTag();

        var result = tag.Approve();

        Assert.False(result.IsError);
        Assert.Equal(TagStatus.Active, tag.Status);
    }

    [Fact]
    public void RecordProfileAdded_IncrementsCountsAndBumpsLastUsed()
    {
        var tag = NewTag();
        var later = Now.AddHours(1);

        tag.RecordProfileAdded(later);

        Assert.Equal(1, tag.ProfileCount);
        Assert.Equal(1, tag.TotalCount);
        Assert.Equal(later, tag.LastUsedAt);
    }

    [Fact]
    public void RecordProfileRemoved_DecrementsButNeverGoesNegative()
    {
        var tag = NewTag();
        tag.RecordProfileAdded(Now);

        tag.RecordProfileRemoved();
        tag.RecordProfileRemoved();

        Assert.Equal(0, tag.ProfileCount);
        Assert.Equal(0, tag.TotalCount);
    }
}
