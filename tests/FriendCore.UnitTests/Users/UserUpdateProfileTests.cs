using FriendCore.Domain.Users;

namespace FriendCore.UnitTests.Users;

public class UserUpdateProfileTests
{
    private static readonly DateTimeOffset UpdatedAt = new(2026, 6, 2, 9, 0, 0, TimeSpan.Zero);

    private static User SeededUser()
    {
        var user = UserTestFactory.Create();
        user.UpdateProfile(
            displayName: null,
            bio: "Original bio",
            websiteUrl: "https://alice.example",
            location: "Berlin",
            updatedAt: UserTestFactory.DefaultCreatedAt);
        return user;
    }

    [Fact]
    public void UpdateProfile_LeavesUnsentFieldsUntouched_WhenOnlyBioProvided()
    {
        var user = SeededUser();

        var result = user.UpdateProfile(
            displayName: null,
            bio: "New bio",
            websiteUrl: null,
            location: null,
            updatedAt: UpdatedAt);

        Assert.False(result.IsError);
        Assert.Equal("New bio", user.Profile.Bio);
        Assert.Equal("https://alice.example", user.Profile.WebsiteUrl);
        Assert.Equal("Berlin", user.Profile.Location);
        Assert.Equal(UpdatedAt, user.UpdatedAt);
    }

    [Fact]
    public void UpdateProfile_ClearsField_WhenEmptyStringProvided()
    {
        var user = SeededUser();

        user.UpdateProfile(
            displayName: null,
            bio: "   ",
            websiteUrl: null,
            location: null,
            updatedAt: UpdatedAt);

        Assert.Null(user.Profile.Bio);
        Assert.Equal("https://alice.example", user.Profile.WebsiteUrl);
        Assert.Equal("Berlin", user.Profile.Location);
    }

    [Fact]
    public void UpdateProfile_LeavesDisplayNameUntouched_WhenNull()
    {
        var user = SeededUser();
        var originalDisplayName = user.DisplayName;

        user.UpdateProfile(
            displayName: null,
            bio: "New bio",
            websiteUrl: null,
            location: null,
            updatedAt: UpdatedAt);

        Assert.Equal(originalDisplayName, user.DisplayName);
    }

    [Fact]
    public void UpdateProfile_ReturnsError_WhenDisplayNameBlank()
    {
        var user = SeededUser();

        var result = user.UpdateProfile(
            displayName: "   ",
            bio: null,
            websiteUrl: null,
            location: null,
            updatedAt: UpdatedAt);

        Assert.True(result.IsError);
        Assert.Equal("User.InvalidDisplayName", result.FirstError.Code);
    }
}
