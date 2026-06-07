using FriendCore.Domain.Tags;
using FriendCore.UnitTests.Users;

namespace FriendCore.UnitTests.Tags;

public class UserProfileTagsTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 2, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AddProfileTag_AddsTagWithTierAndVisibility()
    {
        var user = UserTestFactory.Create();
        var tagId = Ulid.NewUlid();

        var result = user.AddProfileTag(tagId, TagTier.Primary, isPublic: true, Now);

        Assert.False(result.IsError);
        var added = Assert.Single(user.ProfileTags);
        Assert.Equal(tagId, added.TagId);
        Assert.Equal(TagTier.Primary, added.Tier);
        Assert.True(added.IsPublic);
        Assert.Equal(Now, user.UpdatedAt);
    }

    [Fact]
    public void AddProfileTag_RejectsDuplicateTag()
    {
        var user = UserTestFactory.Create();
        var tagId = Ulid.NewUlid();
        user.AddProfileTag(tagId, TagTier.Primary, isPublic: true, Now);

        var result = user.AddProfileTag(tagId, TagTier.Secondary, isPublic: false, Now);

        Assert.True(result.IsError);
        Assert.Equal("Tag.Duplicate", result.FirstError.Code);
        Assert.Single(user.ProfileTags);
    }

    [Fact]
    public void AddProfileTag_EnforcesPrimaryLimit()
    {
        var user = UserTestFactory.Create();
        for (var i = 0; i < TagLimits.MaxPrimaryProfileTags; i++)
            Assert.False(user.AddProfileTag(Ulid.NewUlid(), TagTier.Primary, isPublic: true, Now).IsError);

        var overflow = user.AddProfileTag(Ulid.NewUlid(), TagTier.Primary, isPublic: true, Now);

        Assert.True(overflow.IsError);
        Assert.Equal("Tag.TooManyPrimary", overflow.FirstError.Code);
        Assert.Equal(TagLimits.MaxPrimaryProfileTags, user.ProfileTags.Count);
    }

    [Fact]
    public void AddProfileTag_PrimaryLimitDoesNotBlockSecondary()
    {
        var user = UserTestFactory.Create();
        for (var i = 0; i < TagLimits.MaxPrimaryProfileTags; i++)
            user.AddProfileTag(Ulid.NewUlid(), TagTier.Primary, isPublic: true, Now);

        var secondary = user.AddProfileTag(Ulid.NewUlid(), TagTier.Secondary, isPublic: true, Now);

        Assert.False(secondary.IsError);
    }

    [Fact]
    public void AddProfileTag_EnforcesSecondaryLimit()
    {
        var user = UserTestFactory.Create();
        for (var i = 0; i < TagLimits.MaxSecondaryProfileTags; i++)
            user.AddProfileTag(Ulid.NewUlid(), TagTier.Secondary, isPublic: true, Now);

        var overflow = user.AddProfileTag(Ulid.NewUlid(), TagTier.Secondary, isPublic: true, Now);

        Assert.True(overflow.IsError);
        Assert.Equal("Tag.TooManySecondary", overflow.FirstError.Code);
    }

    [Fact]
    public void RemoveProfileTag_RemovesWhenPresent_AndReportsMissing()
    {
        var user = UserTestFactory.Create();
        var tagId = Ulid.NewUlid();
        user.AddProfileTag(tagId, TagTier.Primary, isPublic: true, Now);

        Assert.True(user.RemoveProfileTag(tagId));
        Assert.Empty(user.ProfileTags);
        Assert.False(user.RemoveProfileTag(tagId));
    }
}
