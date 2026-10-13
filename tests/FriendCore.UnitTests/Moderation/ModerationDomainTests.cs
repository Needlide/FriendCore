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

/// <summary>
///     The subject snapshot and, more importantly, its retention rule — which is the part with a
///     policy judgement baked into it and therefore the part worth pinning down precisely.
/// </summary>
public class ReportSubjectSnapshotTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 9, 0, 0, TimeSpan.Zero);
    private static readonly Ulid ReporterId = Ulid.NewUlid();
    private static readonly Ulid TargetId = Ulid.NewUlid();
    private static readonly Ulid ModeratorId = Ulid.NewUlid();

    private static Report ReportWithSnapshot(ReportCategory category) =>
        Report.Create(
                ReporterId, ReportTargetType.Post, TargetId, category, null, Now,
                new ReportSubjectSnapshot(ReportSubjectKind.Text, "the reported text", ReporterId, "alice", Now))
            .Value;

    [Fact]
    public void Create_StoresTheSnapshot()
    {
        var report = ReportWithSnapshot(ReportCategory.Harassment);

        Assert.True(report.HasSubjectSnapshot);
        Assert.Equal(ReportSubjectKind.Text, report.SubjectKind);
        Assert.Equal("the reported text", report.SubjectContent);
        Assert.Equal("alice", report.SubjectAuthorUsername);
    }

    [Fact]
    public void Create_WithoutASubject_LeavesTheSnapshotNullRatherThanBlank()
    {
        // A null snapshot means "the subject was already gone". A blank string would be
        // indistinguishable from a captured empty caption.
        var report = Report.Create(
            ReporterId, ReportTargetType.Post, TargetId, ReportCategory.Harassment, null, Now).Value;

        Assert.False(report.HasSubjectSnapshot);
        Assert.Null(report.SubjectContent);
        Assert.Null(report.SubjectKind);
    }

    [Theory]
    [InlineData(ReportCategory.Spam)]
    [InlineData(ReportCategory.Harassment)]
    [InlineData(ReportCategory.Copyright)]
    [InlineData(ReportCategory.FlashingLights)]
    [InlineData(ReportCategory.Other)]
    public void An_Ordinary_Report_DropsItsSnapshotOnResolve(ReportCategory category)
    {
        var report = ReportWithSnapshot(category);

        report.Resolve(ModeratorId, Now.AddHours(1));

        // Keeping a copy of reported content after a moderator has looked at it means holding
        // content nobody needs, about someone who did nothing wrong enough to justify it.
        Assert.False(report.HasSubjectSnapshot);
    }

    [Theory]
    [InlineData(ReportCategory.InvolvesMinor)]
    [InlineData(ReportCategory.NudityOrSexual)]
    [InlineData(ReportCategory.ViolenceOrDangerous)]
    public void An_Illegal_Report_KeepsItsSnapshotOnResolve(ReportCategory category)
    {
        var report = ReportWithSnapshot(category);

        report.Resolve(ModeratorId, Now.AddHours(1));

        // The carve-out: for these the snapshot IS the evidence, and destroying it on resolution
        // would destroy the record of an offence.
        Assert.True(report.HasSubjectSnapshot);
        Assert.Equal("the reported text", report.SubjectContent);
    }

    [Theory]
    [InlineData(ReportCategory.InvolvesMinor)]
    [InlineData(ReportCategory.Spam)]
    public void Dismissal_AppliesTheSameRuleAsResolution(ReportCategory category)
    {
        var report = ReportWithSnapshot(category);

        report.Dismiss(ModeratorId, Now.AddHours(1));

        Assert.Equal(Report.RequiresEvidenceRetention(category), report.HasSubjectSnapshot);
    }

    [Fact]
    public void Retention_IsDecidedByTheCategoryAlone()
    {
        Assert.True(Report.RequiresEvidenceRetention(ReportCategory.InvolvesMinor));
        Assert.False(Report.RequiresEvidenceRetention(ReportCategory.Spam));
    }
}
