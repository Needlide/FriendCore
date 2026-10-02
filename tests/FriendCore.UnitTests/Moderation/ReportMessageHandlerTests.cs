using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Moderation.Commands.ReportMessage;
using FriendCore.Application.Moderation.Common;
using FriendCore.Domain.Messaging;
using FriendCore.Domain.Moderation;
using FriendCore.Domain.Users;
using NSubstitute;

namespace FriendCore.UnitTests.Moderation;

public class ReportMessageHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IMessagingRepository _messagingRepository = Substitute.For<IMessagingRepository>();
    private readonly IBlockRepository _blockRepository = Substitute.For<IBlockRepository>();
    private readonly IReportRepository _reportRepository = Substitute.For<IReportRepository>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();

    private readonly Ulid _reporterId = Ulid.NewUlid();
    private readonly Ulid _senderId = Ulid.NewUlid();
    private readonly Conversation _conversation;
    private readonly Message _message;

    public ReportMessageHandlerTests()
    {
        _currentUserService.UserId.Returns(_reporterId);
        _dateTimeProvider.UtcNow.Returns(Now);

        _conversation = Conversation.CreateDirect(_reporterId, _senderId, Now).Value;
        _message = Message.Create(_conversation.Id, _senderId, "something awful", Now).Value;

        _messagingRepository.GetByIdAsync(_conversation.Id, Arg.Any<CancellationToken>())
            .Returns(_conversation);
        _messagingRepository.GetMessageAsync(_conversation.Id, _message.Id, Arg.Any<CancellationToken>())
            .Returns(_message);
    }

    private ReportMessageCommandHandler Handler() =>
        new(_currentUserService, _messagingRepository, _blockRepository, _reportRepository, _dateTimeProvider);

    private Task<ErrorOr<Success>> Report() =>
        Handler().Handle(
            new ReportMessageCommand(_conversation.Id, _message.Id, ReportCategoryInput.Harassment, "not ok"),
            CancellationToken.None);

    [Fact]
    public async Task ReturnsUnauthorized_WhenNoCurrentUser()
    {
        _currentUserService.UserId.Returns((Ulid?)null);

        var result = await Report();

        Assert.Equal("Common.Unauthorized", result.FirstError.Code);
    }

    [Fact]
    public async Task FilesAMessageReport_ForTheRecipient()
    {
        var result = await Report();

        Assert.False(result.IsError);
        await _reportRepository.Received(1).AddAsync(
            Arg.Is<Report>(r =>
                r.ReporterId == _reporterId
                && r.TargetType == ReportTargetType.Message
                && r.TargetId == _message.Id
                && r.Category == ReportCategory.Harassment
                // Priority is derived from the category, never taken from the client.
                && r.Priority == ReportPriority.Medium
                && r.Status == ReportStatus.Pending),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefusesToReportYourOwnMessage()
    {
        var mine = Message.Create(_conversation.Id, _reporterId, "never mind", Now).Value;
        _messagingRepository.GetMessageAsync(_conversation.Id, _message.Id, Arg.Any<CancellationToken>())
            .Returns(mine);

        var result = await Report();

        // Otherwise a user could flood the queue with self-reports.
        Assert.Equal("Report.CannotReportSelf", result.FirstError.Code);
        await _reportRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task ReturnsMessageNotFound_ForAMessageInAnotherConversation()
    {
        var elsewhere = Conversation.CreateDirect(_reporterId, Ulid.NewUlid(), Now).Value;

        var result = await Handler().Handle(
            new ReportMessageCommand(elsewhere.Id, _message.Id, ReportCategoryInput.Spam, null),
            CancellationToken.None);

        // The lookup is conversation-scoped, so a guessed id learns nothing.
        Assert.Equal("Messaging.MessageNotFound", result.FirstError.Code);
    }

    [Fact]
    public async Task ReturnsConversationNotFound_ForANonParticipant()
    {
        _currentUserService.UserId.Returns(Ulid.NewUlid());

        var result = await Report();

        Assert.Equal("Messaging.ConversationNotFound", result.FirstError.Code);
    }

    [Fact]
    public async Task ReturnsConversationNotFound_WhenBlocked()
    {
        // §8.2: blocking ends your access to the conversation, so it ends your ability to
        // report into it too.
        _blockRepository.ExistsBetweenAsync(_reporterId, _senderId, Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await Report();

        Assert.Equal("Messaging.ConversationNotFound", result.FirstError.Code);
    }

    [Fact]
    public async Task IsIdempotent_ForADuplicatePendingReport()
    {
        _reportRepository.PendingExistsAsync(
                _reporterId, ReportTargetType.Message, _message.Id, Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await Report();

        // Silent no-op, exactly like post and user reports — a second attempt is not an error.
        Assert.False(result.IsError);
        await _reportRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }
}