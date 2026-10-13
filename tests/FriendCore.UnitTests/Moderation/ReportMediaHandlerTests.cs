using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Moderation.Commands.ReportMedia;
using FriendCore.Application.Moderation.Common;
using FriendCore.Application.Moderation.Services;
using FriendCore.Domain.Media;
using FriendCore.Domain.Moderation;
using NSubstitute;

namespace FriendCore.UnitTests.Moderation;

/// <summary>
/// Reporting an image. Two things have to happen together, and the old
/// <c>{flashing, intenseMotion}</c> shortcut only did the first: it gated the media for
/// opted-in readers but created no Report, so §7.4's layer 2 never reached a moderator.
/// </summary>
public class ReportMediaHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IMediaRepository _mediaRepository = Substitute.For<IMediaRepository>();
    private readonly IReportRepository _reportRepository = Substitute.For<IReportRepository>();
    private readonly ISubjectSnapshotFactory _snapshotFactory = Substitute.For<ISubjectSnapshotFactory>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();

    private readonly Ulid _reporterId = Ulid.NewUlid();
    private readonly Ulid _uploaderId = Ulid.NewUlid();
    private readonly MediaItem _media;

    public ReportMediaHandlerTests()
    {
        _currentUserService.UserId.Returns(_reporterId);
        _dateTimeProvider.UtcNow.Returns(Now);
        _media = MediaItem.Create(
            _uploaderId, "x.png", "image/png", "png",
            hasFlashingWarning: false, hasIntenseMotionWarning: false, Now);
        _mediaRepository.GetByIdAsync(_media.Id, Arg.Any<CancellationToken>()).Returns(_media);
    }

    private ReportMediaCommandHandler Handler() =>
        new(_currentUserService, _mediaRepository, _reportRepository, _snapshotFactory, _dateTimeProvider);

    private Task<ErrorOr<Success>> Report(ReportCategoryInput category = ReportCategoryInput.FlashingLights) =>
        Handler().Handle(new ReportMediaCommand(_media.Id, category, "harmful"), CancellationToken.None);

    [Fact]
    public async Task ReturnsUnauthorized_WhenNoCurrentUser()
    {
        _currentUserService.UserId.Returns((Ulid?)null);

        var result = await Report();

        Assert.Equal("Common.Unauthorized", result.FirstError.Code);
    }

    [Fact]
    public async Task ReturnsNotFound_WhenTheMediaIsMissing()
    {
        _mediaRepository.GetByIdAsync(Arg.Any<Ulid>(), Arg.Any<CancellationToken>()).Returns((MediaItem?)null);

        var result = await Report();

        Assert.Equal("Media.NotFound", result.FirstError.Code);
    }

    [Fact]
    public async Task RefusesToReportYourOwnUpload()
    {
        var mine = MediaItem.Create(
            _reporterId, "x.png", "image/png", "png",
            hasFlashingWarning: false, hasIntenseMotionWarning: false, Now);
        _mediaRepository.GetByIdAsync(mine.Id, Arg.Any<CancellationToken>()).Returns(mine);

        var result = await Handler().Handle(
            new ReportMediaCommand(mine.Id, ReportCategoryInput.FlashingLights, null),
            CancellationToken.None);

        Assert.Equal("Report.CannotReportSelf", result.FirstError.Code);
        await _reportRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task FilesTheReport_AndGatesTheMediaInOneGo()
    {
        var result = await Report();

        Assert.False(result.IsError);

        // The queue row — the half that was missing entirely before.
        await _reportRepository.Received(1).AddAsync(
            Arg.Is<Report>(r =>
                r.ReporterId == _reporterId
                && r.TargetType == ReportTargetType.Media
                && r.TargetId == _media.Id
                && r.Category == ReportCategory.FlashingLights
                && r.Status == ReportStatus.Pending),
            Arg.Any<CancellationToken>());

        // And the gate, so a report nobody triages for hours cannot leave harmful media in
        // front of the readers who opted out of it.
        Assert.True(_media.HasFlashingWarning);
        Assert.True(_media.HasIntenseMotionWarning);
        Assert.Equal(MediaSafetySource.UserReports, _media.SafetySource);
        Assert.Equal(Now, _media.SafetyFlaggedAt);
    }

    [Theory]
    [InlineData(ReportCategoryInput.Copyright)]
    [InlineData(ReportCategoryInput.NudityOrSexual)]
    [InlineData(ReportCategoryInput.Spam)]
    public async Task FilesTheReport_WithoutGatingForNonVisualCategories(ReportCategoryInput category)
    {
        // A copyright report is not evidence that the image flickers. Flagging it as though it
        // did would put a falsehood into the safety data every reader's preference reads.
        var result = await Report(category);

        Assert.False(result.IsError);
        Assert.False(_media.HasFlashingWarning);
        Assert.False(_media.HasIntenseMotionWarning);
        Assert.Equal(MediaSafetySource.None, _media.SafetySource);
        await _reportRepository.Received(1).AddAsync(
            Arg.Is<Report>(r => r.TargetType == ReportTargetType.Media), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IsIdempotent_ForADuplicatePendingReport()
    {
        _reportRepository.PendingExistsAsync(
                _reporterId, ReportTargetType.Media, _media.Id, Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await Report();

        Assert.False(result.IsError);
        await _reportRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task DoesNotOverrideAModeratorDecision()
    {
        // FlagSafety treats a Moderator source as authoritative; this asserts we go through it
        // rather than setting the flags directly, which would silently downgrade a moderator.
        _media.FlagSafety(true, true, MediaSafetySource.Moderator, Now.AddHours(-1));

        await Report(ReportCategoryInput.Copyright);

        Assert.Equal(MediaSafetySource.Moderator, _media.SafetySource);
    }
}
