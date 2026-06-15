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
    private readonly Ulid _recipient = Ulid.NewUlid();
    private readonly Ulid _actor = Ulid.NewUlid();

    private NotificationService CreateService() =>
        new(_notificationRepository, _blockRepository, NullLogger<NotificationService>.Instance);

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
}
