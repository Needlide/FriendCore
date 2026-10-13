using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Moderation.Commands.ReportPost;
using FriendCore.Application.Moderation.Commands.ReportUser;
using FriendCore.Application.Moderation.Common;
using FriendCore.Application.Moderation.Services;
using FriendCore.Domain.Moderation;
using FriendCore.Domain.Posts;
using FriendCore.Domain.Users;
using FriendCore.UnitTests.Users;
using NSubstitute;

namespace FriendCore.UnitTests.Moderation;

public class ReportHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 13, 9, 0, 0, TimeSpan.Zero);

    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IPostRepository _postRepository = Substitute.For<IPostRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IReportRepository _reportRepository = Substitute.For<IReportRepository>();
    private readonly ISubjectSnapshotFactory _snapshotFactory = Substitute.For<ISubjectSnapshotFactory>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly Ulid _reporterId = Ulid.NewUlid();

    public ReportHandlerTests()
    {
        _currentUserService.UserId.Returns(_reporterId);
        _dateTimeProvider.UtcNow.Returns(Now);
    }

    private ReportPostCommandHandler PostHandler() =>
        new(_currentUserService, _postRepository, _reportRepository, _snapshotFactory, _dateTimeProvider);

    private ReportUserCommandHandler UserHandler() =>
        new(_currentUserService, _userRepository, _reportRepository, _snapshotFactory, _dateTimeProvider);

    [Fact]
    public async Task ReportPost_ReturnsNotFound_WhenPostMissing()
    {
        _postRepository.GetByIdAsync(Arg.Any<Ulid>(), Arg.Any<CancellationToken>()).Returns((Post?)null);

        var result = await PostHandler().Handle(
            new ReportPostCommand(Ulid.NewUlid(), ReportCategoryInput.Spam, null), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Post.NotFound", result.FirstError.Code);
        await _reportRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task ReportPost_PersistsReport_WithCategoryDerivedPriority()
    {
        var post = Post.Create(Ulid.NewUlid(), "bad post", ContentSensitivity.General, Now).Value;
        _postRepository.GetByIdAsync(post.Id, Arg.Any<CancellationToken>()).Returns(post);

        var result = await PostHandler().Handle(
            new ReportPostCommand(post.Id, ReportCategoryInput.InvolvesMinor, "concerning"), CancellationToken.None);

        Assert.False(result.IsError);
        await _reportRepository.Received(1).AddAsync(
            Arg.Is<Report>(r =>
                r.ReporterId == _reporterId
                && r.TargetType == ReportTargetType.Post
                && r.TargetId == post.Id
                && r.Category == ReportCategory.InvolvesMinor
                && r.Priority == ReportPriority.High
                && r.Status == ReportStatus.Pending),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReportPost_IsIdempotent_WhenPendingReportExists()
    {
        var post = Post.Create(Ulid.NewUlid(), "bad post", ContentSensitivity.General, Now).Value;
        _postRepository.GetByIdAsync(post.Id, Arg.Any<CancellationToken>()).Returns(post);
        _reportRepository.PendingExistsAsync(_reporterId, ReportTargetType.Post, post.Id, Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await PostHandler().Handle(
            new ReportPostCommand(post.Id, ReportCategoryInput.Spam, null), CancellationToken.None);

        Assert.False(result.IsError);
        await _reportRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task ReportUser_ReturnsCannotReportSelf()
    {
        var self = UserTestFactory.Create(username: "selfie");
        _currentUserService.UserId.Returns(self.Id);
        _userRepository.GetByUsernameSlugAsync("selfie", Arg.Any<CancellationToken>()).Returns(self);

        var result = await UserHandler().Handle(
            new ReportUserCommand("selfie", ReportCategoryInput.Harassment, null), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Report.CannotReportSelf", result.FirstError.Code);
        await _reportRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task ReportUser_PersistsReport_AgainstTheTarget()
    {
        var target = UserTestFactory.Create(username: "bob", email: "bob@example.com");
        _userRepository.GetByUsernameSlugAsync("bob", Arg.Any<CancellationToken>()).Returns(target);

        var result = await UserHandler().Handle(
            new ReportUserCommand("bob", ReportCategoryInput.Harassment, "mean"), CancellationToken.None);

        Assert.False(result.IsError);
        await _reportRepository.Received(1).AddAsync(
            Arg.Is<Report>(r =>
                r.TargetType == ReportTargetType.User
                && r.TargetId == target.Id
                && r.Category == ReportCategory.Harassment
                && r.Priority == ReportPriority.Medium),
            Arg.Any<CancellationToken>());
    }
}
