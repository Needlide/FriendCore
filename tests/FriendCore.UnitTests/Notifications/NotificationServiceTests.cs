using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Application.Notifications.Services;
using FriendCore.Domain.Notifications;
using FriendCore.Domain.Users;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FriendCore.UnitTests.Notifications;

public class NotificationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 13, 9, 0, 0, TimeSpan.Zero);

    private readonly INotificationRepository _notificationRepository = Substitute.For<INotificationRepository>();
    private readonly IBlockRepository _blockRepository = Substitute.For<IBlockRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IRealtimeNotifier _realtimeNotifier = Substitute.For<IRealtimeNotifier>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly Ulid _recipient = Ulid.NewUlid();
    private readonly Ulid _actor = Ulid.NewUlid();

    public NotificationServiceTests()
    {
        _dateTimeProvider.UtcNow.Returns(Now);
    }

    private NotificationService CreateService() =>
        new(_notificationRepository, _blockRepository, _userRepository, _realtimeNotifier,
            _dateTimeProvider, NullLogger<NotificationService>.Instance);

    [Fact]
    public async Task CreateAsync_Persists_WhenAllowed()
    {
        await CreateService().CreateAsync(
            _recipient, _actor, NotificationType.NewFollower, _actor, Now, CancellationToken.None);

        await _notificationRepository.Received(1).AddAsync(
            Arg.Is<Notification>(n =>
                n.RecipientId == _recipient
                && n.ActorId == _actor
                && n.Type == NotificationType.NewFollower
                && n.EntityId == _actor
                && !n.IsRead),
            Arg.Any<CancellationToken>());
        await _realtimeNotifier.Received(1).NotificationReceivedAsync(
            _recipient,
            Arg.Is<RealtimeNotification>(payload => payload.Type == "NewFollower"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_SuppressesSelfNotification()
    {
        await CreateService().CreateAsync(
            _actor, _actor, NotificationType.NewLike, Ulid.NewUlid(), Now, CancellationToken.None);

        await _notificationRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        // Self-suppression happens before any block lookup.
        await _blockRepository.DidNotReceiveWithAnyArgs().ExistsBetweenAsync(default, default, default);
    }

    [Fact]
    public async Task CreateAsync_SuppressesBetweenBlockedUsers()
    {
        _blockRepository.ExistsBetweenAsync(_recipient, _actor, Arg.Any<CancellationToken>()).Returns(true);

        await CreateService().CreateAsync(
            _recipient, _actor, NotificationType.NewComment, Ulid.NewUlid(), Now, CancellationToken.None);

        await _notificationRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task CreateAsync_SwallowsRepositoryFailure()
    {
        // Best-effort: a persistence failure must never propagate to the triggering action.
        _notificationRepository.AddAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("db down")));

        var exception = await Record.ExceptionAsync(() => CreateService().CreateAsync(
            _recipient, _actor, NotificationType.NewMessage, Ulid.NewUlid(), Now, CancellationToken.None));

        Assert.Null(exception);
    }

    [Fact]
    public async Task CreateAsync_SkipsMutedType_WithoutTouchingStorageOrPush()
    {
        var preferences = UserPreferences.CreateDefault(_recipient, Now);
        preferences.SetMutedNotificationTypes(["NewFollower"], Now);
        _userRepository.GetPreferencesAsync(_recipient, Arg.Any<CancellationToken>())
            .Returns(preferences);

        await CreateService().CreateAsync(
            _recipient, _actor, NotificationType.NewFollower, _actor, Now, CancellationToken.None);

        await _notificationRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        await _realtimeNotifier.DidNotReceiveWithAnyArgs()
            .NotificationReceivedAsync(default, default!, default);
    }

    [Fact]
    public async Task CreateAsync_CreatesUnmutedType_WhenOthersMuted()
    {
        var preferences = UserPreferences.CreateDefault(_recipient, Now);
        preferences.SetMutedNotificationTypes(["NewFollower"], Now);
        _userRepository.GetPreferencesAsync(_recipient, Arg.Any<CancellationToken>())
            .Returns(preferences);

        await CreateService().CreateAsync(
            _recipient, _actor, NotificationType.NewLike, Ulid.NewUlid(), Now, CancellationToken.None);

        await _notificationRepository.Received(1).AddAsync(
            Arg.Is<Notification>(n => n.Type == NotificationType.NewLike),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetMutedNotificationTypes_DropsUnknownNames()
    {
        var preferences = UserPreferences.CreateDefault(Ulid.NewUlid(), Now);
        preferences.SetMutedNotificationTypes(["NewLike", "Nope", "NewLike"], Now);

        Assert.Equal(["NewLike"], preferences.MutedNotificationTypes);
        Assert.True(preferences.Mutes(NotificationType.NewLike));
        Assert.False(preferences.Mutes(NotificationType.NewFollower));
    }

    [Fact]
    public async Task CreateAsync_SkipsSecondMessage_WhileFirstIsUnread()
    {
        // One ping per sender until read: the recipient already has an unread NewMessage
        // from this actor, so the burst's follow-ups create nothing (and push nothing).
        _userRepository.GetPreferencesAsync(_recipient, Arg.Any<CancellationToken>())
            .Returns(UserPreferences.CreateDefault(_recipient, Now));
        _notificationRepository
            .ExistsUnreadAsync(_recipient, _actor, NotificationType.NewMessage, Arg.Any<CancellationToken>())
            .Returns(true);

        await CreateService().CreateAsync(
            _recipient, _actor, NotificationType.NewMessage, Ulid.NewUlid(), Now, CancellationToken.None);

        await _notificationRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        await _realtimeNotifier.DidNotReceiveWithAnyArgs()
            .NotificationReceivedAsync(default, default!, default);
    }

    [Fact]
    public async Task CreateAsync_CollapseAppliesPerActor_NotGlobally()
    {
        // A pending ping from Alice must not swallow Bob's first message.
        _userRepository.GetPreferencesAsync(_recipient, Arg.Any<CancellationToken>())
            .Returns(UserPreferences.CreateDefault(_recipient, Now));
        _notificationRepository
            .ExistsUnreadAsync(
                _recipient, Arg.Is<Ulid>(id => id != _actor), NotificationType.NewMessage,
                Arg.Any<CancellationToken>())
            .Returns(true);

        await CreateService().CreateAsync(
            _recipient, _actor, NotificationType.NewMessage, Ulid.NewUlid(), Now, CancellationToken.None);

        await _notificationRepository.Received(1).AddAsync(
            Arg.Any<Notification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_DoesNotCollapse_Likes_OnDifferentPosts()
    {
        // Collapsing is messages-only: two likes from one actor on different posts each
        // carry their own information and both are created.
        _userRepository.GetPreferencesAsync(_recipient, Arg.Any<CancellationToken>())
            .Returns(UserPreferences.CreateDefault(_recipient, Now));
        _notificationRepository
            .ExistsUnreadAsync(_recipient, _actor, NotificationType.NewLike, Arg.Any<CancellationToken>())
            .Returns(true);

        await CreateService().CreateAsync(
            _recipient, _actor, NotificationType.NewLike, Ulid.NewUlid(), Now, CancellationToken.None);

        await _notificationRepository.Received(1).AddAsync(
            Arg.Any<Notification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_SkipsEverything_WhileSnoozed()
    {
        var preferences = UserPreferences.CreateDefault(_recipient, Now);
        preferences.SetSnoozedUntil(Now.AddHours(2), Now);
        _userRepository.GetPreferencesAsync(_recipient, Arg.Any<CancellationToken>())
            .Returns(preferences);

        await CreateService().CreateAsync(
            _recipient, _actor, NotificationType.NewMessage, Ulid.NewUlid(), Now, CancellationToken.None);

        await _notificationRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        await _realtimeNotifier.DidNotReceiveWithAnyArgs()
            .NotificationReceivedAsync(default, default!, default);
    }

    [Fact]
    public async Task CreateAsync_Resumes_AfterSnoozeExpiry()
    {
        var preferences = UserPreferences.CreateDefault(_recipient, Now);
        preferences.SetSnoozedUntil(Now.AddHours(-1), Now);
        _userRepository.GetPreferencesAsync(_recipient, Arg.Any<CancellationToken>())
            .Returns(preferences);

        await CreateService().CreateAsync(
            _recipient, _actor, NotificationType.NewLike, Ulid.NewUlid(), Now, CancellationToken.None);

        await _notificationRepository.Received(1).AddAsync(
            Arg.Any<Notification>(), Arg.Any<CancellationToken>());
    }
}
