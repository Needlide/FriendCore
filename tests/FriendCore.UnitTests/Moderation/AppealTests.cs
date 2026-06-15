using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Moderation.Commands.ReviewAppeal;
using FriendCore.Application.Moderation.Commands.SubmitAppeal;
using FriendCore.Application.Moderation.Common;
using FriendCore.Domain.Moderation;
using FriendCore.Domain.Posts;
using FriendCore.Domain.Users;
using FriendCore.UnitTests.Users;
using NSubstitute;

namespace FriendCore.UnitTests.Moderation;

public class AppealTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 13, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_SetsDeadlineBySeverity()
    {
        var ban = Appeal.Create(Ulid.NewUlid(), Ulid.NewUlid(), "unfair", ModerationActionType.PermanentBan, Now).Value;
        var warning = Appeal.Create(Ulid.NewUlid(), Ulid.NewUlid(), "unfair", ModerationActionType.Warning, Now).Value;

        Assert.Equal(Now.AddDays(3), ban.Deadline);
        Assert.Equal(Now.AddDays(7), warning.Deadline);
        Assert.Equal(AppealStatus.Pending, ban.Status);
    }

    [Fact]
    public void Create_RejectsDismissal_AsNotAppealable()
    {
        var result = Appeal.Create(
            Ulid.NewUlid(), Ulid.NewUlid(), "x", ModerationActionType.ReportDismissed, Now);

        Assert.True(result.IsError);
        Assert.Equal("Appeal.NotAppealable", result.FirstError.Code);
    }

    [Fact]
    public void Review_SetsOutcome_AndGuardsAgainstSecondReview()
    {
        var appeal = Appeal.Create(
            Ulid.NewUlid(), Ulid.NewUlid(), "unfair", ModerationActionType.PermanentBan, Now).Value;
        var reviewer = Ulid.NewUlid();

        var first = appeal.Review(AppealStatus.Overturned, reviewer, "agreed", Now);
        Assert.False(first.IsError);
        Assert.Equal(AppealStatus.Overturned, appeal.Status);
        Assert.Equal(reviewer, appeal.ReviewedByModeratorId);

        var second = appeal.Review(AppealStatus.Upheld, Ulid.NewUlid(), "no", Now);
        Assert.True(second.IsError);
        Assert.Equal("Appeal.AlreadyReviewed", second.FirstError.Code);
    }

    [Fact]
    public void Review_RequiresNote()
    {
        var appeal = Appeal.Create(
            Ulid.NewUlid(), Ulid.NewUlid(), "unfair", ModerationActionType.Warning, Now).Value;

        var result = appeal.Review(AppealStatus.Upheld, Ulid.NewUlid(), "  ", Now);

        Assert.True(result.IsError);
        Assert.Equal("Appeal.ReviewNoteRequired", result.FirstError.Code);
    }

    // ---- handler tests ----

    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IAppealRepository _appealRepository = Substitute.For<IAppealRepository>();
    private readonly IAdminAuditLogRepository _auditLogRepository = Substitute.For<IAdminAuditLogRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IPostRepository _postRepository = Substitute.For<IPostRepository>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();

    private SubmitAppealCommandHandler SubmitHandler() =>
        new(_currentUserService, _auditLogRepository, _postRepository, _appealRepository, _dateTimeProvider);

    private ReviewAppealCommandHandler ReviewHandler() =>
        new(_currentUserService, _appealRepository, _auditLogRepository, _userRepository, _postRepository,
            _dateTimeProvider);

    private static AdminAuditLog UserAction(Ulid moderatorId, Ulid targetUserId, ModerationActionType action) =>
        AdminAuditLog.Create(moderatorId, action, ReportTargetType.User, targetUserId, "reason", null, null, Now);

    [Fact]
    public async Task Submit_ReturnsNotAffectedUser_WhenActionTargetsSomeoneElse()
    {
        var me = Ulid.NewUlid();
        _currentUserService.UserId.Returns(me);
        _dateTimeProvider.UtcNow.Returns(Now);
        var action = UserAction(Ulid.NewUlid(), Ulid.NewUlid(), ModerationActionType.PermanentBan);
        _auditLogRepository.GetByIdAsync(action.Id, Arg.Any<CancellationToken>()).Returns(action);

        var result = await SubmitHandler().Handle(new SubmitAppealCommand(action.Id, "let me back"), default);

        Assert.True(result.IsError);
        Assert.Equal("Appeal.NotTheAffectedUser", result.FirstError.Code);
        await _appealRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task Submit_ReturnsAlreadyAppealed_OnDuplicate()
    {
        var me = Ulid.NewUlid();
        _currentUserService.UserId.Returns(me);
        _dateTimeProvider.UtcNow.Returns(Now);
        var action = UserAction(Ulid.NewUlid(), me, ModerationActionType.TemporarySuspension);
        _auditLogRepository.GetByIdAsync(action.Id, Arg.Any<CancellationToken>()).Returns(action);
        _appealRepository.ExistsForActionAsync(action.Id, Arg.Any<CancellationToken>()).Returns(true);

        var result = await SubmitHandler().Handle(new SubmitAppealCommand(action.Id, "again"), default);

        Assert.True(result.IsError);
        Assert.Equal("Appeal.AlreadyAppealed", result.FirstError.Code);
    }

    [Fact]
    public async Task Review_ReturnsSameModerator_WhenReviewerIsOriginalModerator()
    {
        var moderator = Ulid.NewUlid();
        _currentUserService.UserId.Returns(moderator);
        _dateTimeProvider.UtcNow.Returns(Now);
        var action = UserAction(moderator, Ulid.NewUlid(), ModerationActionType.PermanentBan);
        var appeal = Appeal.Create(action.Id, Ulid.NewUlid(), "unfair", ModerationActionType.PermanentBan, Now).Value;
        _appealRepository.GetByIdAsync(appeal.Id, Arg.Any<CancellationToken>()).Returns(appeal);
        _auditLogRepository.GetByIdAsync(action.Id, Arg.Any<CancellationToken>()).Returns(action);

        var result = await ReviewHandler().Handle(
            new ReviewAppealCommand(appeal.Id, AppealOutcomeInput.Overturned, "agreed"), default);

        Assert.True(result.IsError);
        Assert.Equal("Appeal.SameModerator", result.FirstError.Code);
        await _appealRepository.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Review_Overturn_ReinstatesBannedUser()
    {
        var originalModerator = Ulid.NewUlid();
        var reviewer = Ulid.NewUlid();
        _currentUserService.UserId.Returns(reviewer);
        _dateTimeProvider.UtcNow.Returns(Now);
        var bannedUser = UserTestFactory.Create(username: "banned");
        bannedUser.Ban(Now);
        var action = UserAction(originalModerator, bannedUser.Id, ModerationActionType.PermanentBan);
        var appeal = Appeal.Create(action.Id, bannedUser.Id, "unfair", ModerationActionType.PermanentBan, Now).Value;
        _appealRepository.GetByIdAsync(appeal.Id, Arg.Any<CancellationToken>()).Returns(appeal);
        _auditLogRepository.GetByIdAsync(action.Id, Arg.Any<CancellationToken>()).Returns(action);
        _userRepository.GetByIdIgnoringBlocksAsync(bannedUser.Id, Arg.Any<CancellationToken>()).Returns(bannedUser);

        var result = await ReviewHandler().Handle(
            new ReviewAppealCommand(appeal.Id, AppealOutcomeInput.Overturned, "reversed"), default);

        Assert.False(result.IsError);
        Assert.False(bannedUser.IsBanned);
        Assert.Equal(AppealStatus.Overturned, appeal.Status);
        await _appealRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Review_Upheld_DoesNotReverseEnforcement()
    {
        var reviewer = Ulid.NewUlid();
        _currentUserService.UserId.Returns(reviewer);
        _dateTimeProvider.UtcNow.Returns(Now);
        var bannedUser = UserTestFactory.Create(username: "banned");
        bannedUser.Ban(Now);
        var action = UserAction(Ulid.NewUlid(), bannedUser.Id, ModerationActionType.PermanentBan);
        var appeal = Appeal.Create(action.Id, bannedUser.Id, "unfair", ModerationActionType.PermanentBan, Now).Value;
        _appealRepository.GetByIdAsync(appeal.Id, Arg.Any<CancellationToken>()).Returns(appeal);
        _auditLogRepository.GetByIdAsync(action.Id, Arg.Any<CancellationToken>()).Returns(action);

        var result = await ReviewHandler().Handle(
            new ReviewAppealCommand(appeal.Id, AppealOutcomeInput.Upheld, "stands"), default);

        Assert.False(result.IsError);
        Assert.True(bannedUser.IsBanned);
        await _userRepository.DidNotReceiveWithAnyArgs().GetByIdIgnoringBlocksAsync(default, default);
    }
}
