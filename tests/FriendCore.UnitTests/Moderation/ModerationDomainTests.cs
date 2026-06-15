using FriendCore.Domain.Moderation;
using FriendCore.UnitTests.Users;

namespace FriendCore.UnitTests.Moderation;

public class ModerationDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 13, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Suspend_SetsUntil_AndIsSuspendedReflectsTheClock()
    {
        var user = UserTestFactory.Create();
        var until = Now.AddDays(7);

        user.Suspend(until, Now);

        Assert.Equal(until, user.SuspendedUntil);
        Assert.True(user.IsSuspended(Now));
        Assert.True(user.IsSuspended(until.AddSeconds(-1)));
        Assert.False(user.IsSuspended(until));
        Assert.False(user.IsSuspended(until.AddDays(1)));
    }

    [Fact]
    public void Ban_SetsIsBanned()
    {
        var user = UserTestFactory.Create();

        user.Ban(Now);

        Assert.True(user.IsBanned);
    }

    [Fact]
    public void IsSuspended_FalseByDefault()
    {
        var user = UserTestFactory.Create();

        Assert.False(user.IsSuspended(Now));
        Assert.Null(user.SuspendedUntil);
    }

    [Fact]
    public void Report_Resolve_SetsStatusAndModerator()
    {
        var report = Report.Create(
            Ulid.NewUlid(), ReportTargetType.Post, Ulid.NewUlid(), ReportCategory.Spam, null, Now).Value;
        var moderatorId = Ulid.NewUlid();

        var result = report.Resolve(moderatorId, Now);

        Assert.False(result.IsError);
        Assert.Equal(ReportStatus.Resolved, report.Status);
        Assert.Equal(moderatorId, report.ResolvedByModeratorId);
        Assert.Equal(Now, report.ResolvedAt);
    }

    [Fact]
    public void Report_Dismiss_SetsDismissed()
    {
        var report = Report.Create(
            Ulid.NewUlid(), ReportTargetType.User, Ulid.NewUlid(), ReportCategory.Other, null, Now).Value;

        var result = report.Dismiss(Ulid.NewUlid(), Now);

        Assert.False(result.IsError);
        Assert.Equal(ReportStatus.Dismissed, report.Status);
    }

    [Fact]
    public void Report_Resolve_Twice_ReturnsAlreadyResolved()
    {
        var report = Report.Create(
            Ulid.NewUlid(), ReportTargetType.Post, Ulid.NewUlid(), ReportCategory.Spam, null, Now).Value;
        report.Resolve(Ulid.NewUlid(), Now);

        var second = report.Resolve(Ulid.NewUlid(), Now);

        Assert.True(second.IsError);
        Assert.Equal("Report.AlreadyResolved", second.FirstError.Code);
    }
}
