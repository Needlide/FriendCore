using FriendCore.Domain.Users;

namespace FriendCore.UnitTests.Users;

public class UserMarkEmailVerifiedTests
{
    private static readonly DateTimeOffset VerifiedAt = new(2026, 6, 2, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void MarkEmailVerified_SetsVerifiedAndUpdatesTimestamp_WhenNotVerified()
    {
        var user = UserTestFactory.Create();

        var result = user.MarkEmailVerified(VerifiedAt);

        Assert.False(result.IsError);
        Assert.True(user.IsVerified);
        Assert.Equal(VerifiedAt, user.UpdatedAt);
    }

    [Fact]
    public void MarkEmailVerified_ReturnsConflict_WhenAlreadyVerified()
    {
        var user = UserTestFactory.Create();
        user.MarkEmailVerified(VerifiedAt);

        var result = user.MarkEmailVerified(VerifiedAt.AddHours(1));

        Assert.True(result.IsError);
        Assert.Equal(UserErrors.EmailAlreadyVerified.Code, result.FirstError.Code);
    }
}
