using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Moderation.Services;
using FriendCore.Application.Notifications.Services;
using FriendCore.Domain.Media;
using FriendCore.Domain.Messaging;
using FriendCore.Domain.Moderation;
using FriendCore.Domain.Posts;
using FriendCore.Domain.Users;
using FriendCore.UnitTests.Users;
using NSubstitute;

namespace FriendCore.UnitTests.Moderation;

public class ModerationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 13, 9, 0, 0, TimeSpan.Zero);

    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IPostRepository _postRepository = Substitute.For<IPostRepository>();
    private readonly IMessagingRepository _messagingRepository = Substitute.For<IMessagingRepository>();
    private readonly IMediaRepository _mediaRepository = Substitute.For<IMediaRepository>();
    private readonly IAdminAuditLogRepository _auditLogRepository = Substitute.For<IAdminAuditLogRepository>();
    private readonly INotificationService _notificationService = Substitute.For<INotificationService>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly Ulid _moderatorId = Ulid.NewUlid();

    public ModerationServiceTests()
    {
        _currentUserService.UserId.Returns(_moderatorId);
        _currentUserService.IpAddress.Returns("203.0.113.7");
        _dateTimeProvider.UtcNow.Returns(Now);
    }

    private ModerationService CreateService() =>
        new(_currentUserService, _userRepository, _postRepository, _messagingRepository,
            _mediaRepository, _auditLogRepository, _notificationService, _dateTimeProvider);

    private static Report PostReport(Ulid postId) =>
        Report.Create(Ulid.NewUlid(), ReportTargetType.Post, postId, ReportCategory.Spam, null, Now).Value;

    [Fact]
    public async Task ContentRemoval_StagesSoftDelete_ResolvesReport_AndLogs()
    {
        var author = UserTestFactory.Create(username: "author");
        var post = Post.Create(author.Id, "bad", ContentSensitivity.General, Now).Value;
        var report = PostReport(post.Id);
        _postRepository.GetByIdForModerationAsync(post.Id, Arg.Any<CancellationToken>()).Returns(post);

        var result = await CreateService().ExecuteAsync(
            ModerationActionType.ContentRemoval, ReportTargetType.Post, post.Id, "rule 1", null, report,
            CancellationToken.None);

        Assert.False(result.IsError);
        _postRepository.Received(1).StageSoftDelete(post);
        Assert.Equal(ReportStatus.Resolved, report.Status);
        await _auditLogRepository.Received(1).AddAndSaveAsync(
            Arg.Is<AdminAuditLog>(e =>
                e.Action == ModerationActionType.ContentRemoval
                && e.TargetType == ReportTargetType.Post
                && e.TargetId == post.Id
                && e.Reason == "rule 1"
                && e.IpAddress == "203.0.113.7"
                && e.RelatedReportId == report.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ContentRemoval_OnUserTarget_ReturnsError()
    {
        var result = await CreateService().ExecuteAsync(
            ModerationActionType.ContentRemoval, ReportTargetType.User, Ulid.NewUlid(), "x", null, null,
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Moderation.ContentRemovalRequiresContent", result.FirstError.Code);
        await _auditLogRepository.DidNotReceiveWithAnyArgs().AddAndSaveAsync(default!, default);
    }

    [Fact]
    public async Task Suspension_SuspendsTheUser_ForTheGivenDays()
    {
        var user = UserTestFactory.Create(username: "bob");
        _userRepository.GetByIdIgnoringBlocksAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var result = await CreateService().ExecuteAsync(
            ModerationActionType.TemporarySuspension, ReportTargetType.User, user.Id, "spam", 7, null,
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(Now.AddDays(7), user.SuspendedUntil);
        await _auditLogRepository.Received(1).AddAndSaveAsync(
            Arg.Is<AdminAuditLog>(e => e.Action == ModerationActionType.TemporarySuspension && e.TargetId == user.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Suspension_WithoutDays_ReturnsError_AndDoesNotLog()
    {
        var user = UserTestFactory.Create(username: "bob");
        _userRepository.GetByIdIgnoringBlocksAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var result = await CreateService().ExecuteAsync(
            ModerationActionType.TemporarySuspension, ReportTargetType.User, user.Id, "spam", null, null,
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Moderation.InvalidSuspensionDuration", result.FirstError.Code);
        Assert.Null(user.SuspendedUntil);
        await _auditLogRepository.DidNotReceiveWithAnyArgs().AddAndSaveAsync(default!, default);
    }

    [Fact]
    public async Task Ban_OnPostReport_BansThePostAuthor()
    {
        var author = UserTestFactory.Create(username: "author");
        var post = Post.Create(author.Id, "bad", ContentSensitivity.General, Now).Value;
        var report = PostReport(post.Id);
        _postRepository.GetByIdForModerationAsync(post.Id, Arg.Any<CancellationToken>()).Returns(post);
        _userRepository.GetByIdIgnoringBlocksAsync(author.Id, Arg.Any<CancellationToken>()).Returns(author);

        var result = await CreateService().ExecuteAsync(
            ModerationActionType.PermanentBan, ReportTargetType.Post, post.Id, "severe", null, report,
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.True(author.IsBanned);
        Assert.Equal(ReportStatus.Resolved, report.Status);
        await _auditLogRepository.Received(1).AddAndSaveAsync(
            Arg.Is<AdminAuditLog>(e => e.TargetType == ReportTargetType.User && e.TargetId == author.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Dismiss_ResolvesReportAsDismissed_WithoutEnforcement()
    {
        var post = Post.Create(Ulid.NewUlid(), "ok", ContentSensitivity.General, Now).Value;
        var report = PostReport(post.Id);

        var result = await CreateService().ExecuteAsync(
            ModerationActionType.ReportDismissed, ReportTargetType.Post, post.Id, "no violation", null, report,
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(ReportStatus.Dismissed, report.Status);
        _postRepository.DidNotReceiveWithAnyArgs().StageSoftDelete(default!);
        await _auditLogRepository.Received(1).AddAndSaveAsync(
            Arg.Is<AdminAuditLog>(e => e.Action == ModerationActionType.ReportDismissed),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AlreadyResolvedReport_ReturnsConflict_AndDoesNotLog()
    {
        var user = UserTestFactory.Create(username: "bob");
        _userRepository.GetByIdIgnoringBlocksAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        var report = Report.Create(
            Ulid.NewUlid(), ReportTargetType.User, user.Id, ReportCategory.Harassment, null, Now).Value;
        report.Resolve(Ulid.NewUlid(), Now); // already resolved

        var result = await CreateService().ExecuteAsync(
            ModerationActionType.PermanentBan, ReportTargetType.User, user.Id, "late", null, report,
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Report.AlreadyResolved", result.FirstError.Code);
        // Enforcement was staged on the tracked entity but never committed (no save).
        await _auditLogRepository.DidNotReceiveWithAnyArgs().AddAndSaveAsync(default!, default);
    }

    [Fact]
    public async Task ContentRemoval_OnMessageTarget_StagesTheSoftDelete_AndLogsAgainstTheMessage()
    {
        var message = Message.Create(Ulid.NewUlid(), Ulid.NewUlid(), "abusive", Now).Value;
        var report = Report.Create(
            Ulid.NewUlid(), ReportTargetType.Message, message.Id, ReportCategory.Harassment, null, Now).Value;
        _messagingRepository.GetMessageForModerationAsync(message.Id, Arg.Any<CancellationToken>())
            .Returns(message);

        var result = await CreateService().ExecuteAsync(
            ModerationActionType.ContentRemoval, ReportTargetType.Message, message.Id, "rule 1", null, report,
            CancellationToken.None);

        Assert.False(result.IsError);
        // The removal and the audit entry commit in one SaveChanges (§10.3).
        _messagingRepository.Received(1).StageSoftDeleteAsync(message, Now, MessageDeletionSource.Moderation, Arg.Any<CancellationToken>());
        Assert.Equal(ReportStatus.Resolved, report.Status);
        await _auditLogRepository.Received(1).AddAndSaveAsync(
            Arg.Is<AdminAuditLog>(e =>
                e.Action == ModerationActionType.ContentRemoval
                && e.TargetType == ReportTargetType.Message
                && e.TargetId == message.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ContentRemoval_OnMessageTarget_ReturnsTargetNotFound_WhenTheMessageIsGone()
    {
        _messagingRepository.GetMessageForModerationAsync(Arg.Any<Ulid>(), Arg.Any<CancellationToken>())
            .Returns((Message?)null);

        var result = await CreateService().ExecuteAsync(
            ModerationActionType.ContentRemoval, ReportTargetType.Message, Ulid.NewUlid(), "x", null, null,
            CancellationToken.None);

        Assert.Equal("Moderation.TargetNotFound", result.FirstError.Code);
        await _auditLogRepository.DidNotReceiveWithAnyArgs().AddAndSaveAsync(default!, default);
    }

    [Fact]
    public async Task Suspension_OnMessageReport_SuspendsTheSender_NotTheReporter()
    {
        var sender = UserTestFactory.Create(username: "sender");
        var message = Message.Create(Ulid.NewUlid(), sender.Id, "abusive", Now).Value;
        _messagingRepository.GetMessageForModerationAsync(message.Id, Arg.Any<CancellationToken>())
            .Returns(message);
        _userRepository.GetByIdIgnoringBlocksAsync(sender.Id, Arg.Any<CancellationToken>()).Returns(sender);

        var result = await CreateService().ExecuteAsync(
            ModerationActionType.TemporarySuspension, ReportTargetType.Message, message.Id, "abuse", 7, null,
            CancellationToken.None);

        Assert.False(result.IsError);
        // The action lands on whoever wrote the message — a suspension must not touch the person
        // who reported it, nor fail because the target is a message rather than a post.
        Assert.NotNull(sender.SuspendedUntil);
        Assert.Equal(Now.AddDays(7), sender.SuspendedUntil);
    }

    [Fact]
    public async Task ContentRemoval_OnMediaTarget_SoftDeletesTheImage_AndLogsAgainstIt()
    {
        var media = MediaItem.Create(
            UserTestFactory.Create(username: "uploader").Id, "a.png", "image/png", ".png",
            MediaSafetyFlags.Empty, Now);
        var report = Report.Create(
            Ulid.NewUlid(), ReportTargetType.Media, media.Id, ReportCategory.FlashingLights, null, Now).Value;
        _mediaRepository.GetByIdForModerationAsync(media.Id, Arg.Any<CancellationToken>())
            .Returns(media);

        var result = await CreateService().ExecuteAsync(
            ModerationActionType.ContentRemoval, ReportTargetType.Media, media.Id, "harmful", null, report,
            CancellationToken.None);

        Assert.False(result.IsError);
        // Removing the media is what takes the image out of circulation — posts and messages
        // reference it, so the row going away is what every reader actually sees.
        _mediaRepository.Received(1).StageSoftDelete(media);
        await _auditLogRepository.Received(1).AddAndSaveAsync(
            Arg.Is<AdminAuditLog>(e =>
                e.Action == ModerationActionType.ContentRemoval
                && e.TargetType == ReportTargetType.Media
                && e.TargetId == media.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Suspension_OnMediaReport_HitsTheUploader()
    {
        var uploader = UserTestFactory.Create(username: "uploader");
        var media = MediaItem.Create(
            uploader.Id, "a.png", "image/png", ".png",
            MediaSafetyFlags.Empty, Now);
        _mediaRepository.GetByIdForModerationAsync(media.Id, Arg.Any<CancellationToken>())
            .Returns(media);
        _userRepository.GetByIdIgnoringBlocksAsync(uploader.Id, Arg.Any<CancellationToken>())
            .Returns(uploader);

        var result = await CreateService().ExecuteAsync(
            ModerationActionType.TemporarySuspension, ReportTargetType.Media, media.Id, "harmful", 7, null,
            CancellationToken.None);

        Assert.False(result.IsError);
        // The uploader, who is not necessarily whoever published the post it landed in.
        Assert.Equal(Now.AddDays(7), uploader.SuspendedUntil);
    }
}
