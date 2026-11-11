using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Users;
using FriendCore.IntegrationTests.Infrastructure;

namespace FriendCore.IntegrationTests.Notifications;

/// <summary>
///     Spec §5.7/§11/§17.2: the four notification types fire from their triggering actions, the
///     bell counts unread and clears on read, and self-actions never notify.
/// </summary>
public class NotificationFlowTests(FriendCoreApiFactory factory) : IClassFixture<FriendCoreApiFactory>
{
    private readonly AuthScenarios _scenarios = new(factory);

    private Task<(HttpClient Client, string Username)> RegisterClient() =>
        new AuthScenarios(factory).RegisterVerifiedClientAsync();

    private async Task<HttpClient> ModeratorClient()
    {
        factory.Clock.Reset();
        var client = factory.CreateClient();
        var token = await _scenarios.RegisterWithRoleAndGetToken(
            client, AuthScenarios.UniqueEmail(), UserRole.Moderator);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<string> CreatePost(HttpClient client, string content)
    {
        var response = await client.PostAsJsonAsync("/api/v1/posts", new { content, sensitivity = "General" });
        var body = await response.Content.ReadFromJsonAsync<PostResponse>();
        return body!.Id;
    }

    private static async Task<NotificationListResponse> GetNotifications(HttpClient client)
    {
        return (await client.GetFromJsonAsync<NotificationListResponse>("/api/v1/notifications"))!;
    }

    private static async Task<int> GetUnreadCount(HttpClient client)
    {
        var body = await client.GetFromJsonAsync<UnreadCountResponse>("/api/v1/notifications/unread-count");
        return body!.Count;
    }

    [Fact]
    public async Task Follow_NotifiesTheFollowedUser()
    {
        var (alice, aliceName) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();

        await alice.PostAsync($"/api/v1/users/{bobName}/follow", content: null);

        var notifications = (await GetNotifications(bob)).Notifications;
        var notification = Assert.Single(notifications);
        Assert.Equal("NewFollower", notification.Type);
        Assert.Equal(aliceName, notification.Actor.Username);
        Assert.False(notification.IsRead);
    }

    [Fact]
    public async Task MutedType_IsNeverCreated_UntilUnmuted()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();

        var mute = await bob.PatchAsJsonAsync(
            "/api/v1/users/me/notification-types", new { mutedTypes = new[] { "NewFollower" } });
        Assert.Equal(HttpStatusCode.OK, mute.StatusCode);

        await alice.PostAsync($"/api/v1/users/{bobName}/follow", content: null);
        Assert.Empty((await GetNotifications(bob)).Notifications);
        Assert.Equal(0, await GetUnreadCount(bob));

        var unmute = await bob.PatchAsJsonAsync(
            "/api/v1/users/me/notification-types", new { mutedTypes = Array.Empty<string>() });
        Assert.Equal(HttpStatusCode.OK, unmute.StatusCode);

        // Unmuting reveals only future events — nothing backfills.
        Assert.Empty((await GetNotifications(bob)).Notifications);

        var (cara, caraName) = await RegisterClient();
        await cara.PostAsync($"/api/v1/users/{bobName}/follow", content: null);
        var notification = Assert.Single((await GetNotifications(bob)).Notifications);
        Assert.Equal(caraName, notification.Actor.Username);
    }

    [Fact]
    public async Task Like_NotifiesThePostAuthor_WithPostAsEntity()
    {
        var (author, _) = await RegisterClient();
        var (liker, likerName) = await RegisterClient();
        var postId = await CreatePost(author, "like me");

        await liker.PostAsync($"/api/v1/posts/{postId}/like", content: null);

        var notification = Assert.Single((await GetNotifications(author)).Notifications);
        Assert.Equal("NewLike", notification.Type);
        Assert.Equal(likerName, notification.Actor.Username);
        Assert.Equal(postId, notification.EntityId);
    }

    [Fact]
    public async Task Comment_NotifiesThePostAuthor()
    {
        var (author, _) = await RegisterClient();
        var (commenter, commenterName) = await RegisterClient();
        var postId = await CreatePost(author, "comment me");

        await commenter.PostAsJsonAsync($"/api/v1/posts/{postId}/comments", new { content = "nice" });

        var notification = Assert.Single((await GetNotifications(author)).Notifications);
        Assert.Equal("NewComment", notification.Type);
        Assert.Equal(commenterName, notification.Actor.Username);
    }

    [Fact]
    public async Task Message_NotifiesTheRecipient()
    {
        var (alice, aliceName) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var start = await alice.PostAsJsonAsync("/api/v1/conversations", new { username = bobName });
        var conversation = await start.Content.ReadFromJsonAsync<ConversationResponse>();

        await alice.PostAsJsonAsync(
            $"/api/v1/conversations/{conversation!.Id}/messages", new { content = "hi bob" });

        var notification = Assert.Single((await GetNotifications(bob)).Notifications);
        Assert.Equal("NewMessage", notification.Type);
        Assert.Equal(aliceName, notification.Actor.Username);
        Assert.Equal(conversation.Id, notification.EntityId);
    }

    [Fact]
    public async Task MessageBurst_CollapsesToOneNotification_UntilRead()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var (cara, caraName) = await RegisterClient();
        var start = await alice.PostAsJsonAsync("/api/v1/conversations", new { username = bobName });
        var conversation = await start.Content.ReadFromJsonAsync<ConversationResponse>();
        var caraStart = await cara.PostAsJsonAsync("/api/v1/conversations", new { username = bobName });
        var caraConversation = await caraStart.Content.ReadFromJsonAsync<ConversationResponse>();

        // Three rapid messages from Alice: one ping, not three.
        for (var i = 0; i < 3; i++)
        {
            var send = await alice.PostAsJsonAsync(
                $"/api/v1/conversations/{conversation!.Id}/messages", new { content = $"m{i}" });
            Assert.Equal(HttpStatusCode.Created, send.StatusCode);
        }
        Assert.Single((await GetNotifications(bob)).Notifications);

        // A first message from a different sender still notifies.
        await cara.PostAsJsonAsync(
            $"/api/v1/conversations/{caraConversation!.Id}/messages", new { content = "hi" });
        var notifications = (await GetNotifications(bob)).Notifications;
        Assert.Equal(2, notifications.Count);
        Assert.Contains(notifications, n => n.Actor.Username == caraName);

        // Reading clears the collapse window: the next message pings again.
        var read = await bob.PostAsync("/api/v1/notifications/read", content: null);
        Assert.Equal(HttpStatusCode.NoContent, read.StatusCode);
        await alice.PostAsJsonAsync(
            $"/api/v1/conversations/{conversation.Id}/messages", new { content = "again" });
        Assert.Equal(1, await GetUnreadCount(bob));
    }

    [Fact]
    public async Task SelfLike_DoesNotNotify()
    {
        var (author, _) = await RegisterClient();
        var postId = await CreatePost(author, "my own post");

        await author.PostAsync($"/api/v1/posts/{postId}/like", content: null);

        Assert.Empty((await GetNotifications(author)).Notifications);
        Assert.Equal(0, await GetUnreadCount(author));
    }

    [Fact]
    public async Task SingleNotification_CanBeMarkedRead_WithoutMarkingTheRest()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var (cara, _) = await RegisterClient();

        await alice.PostAsync($"/api/v1/users/{bobName}/follow", content: null);
        await cara.PostAsync($"/api/v1/users/{bobName}/follow", content: null);
        var notifications = (await GetNotifications(bob)).Notifications;
        Assert.Equal(2, notifications.Count);
        Assert.Equal(2, await GetUnreadCount(bob));

        var read = await bob.PostAsync($"/api/v1/notifications/{notifications[0].Id}/read", content: null);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        var count = await read.Content.ReadFromJsonAsync<UnreadCountResponse>();
        Assert.Equal(1, count!.Count);

        // Only the targeted row changed; the badge follows the server's authoritative count.
        var after = (await GetNotifications(bob)).Notifications;
        Assert.True(after.Single(n => n.Id == notifications[0].Id).IsRead);
        Assert.False(after.Single(n => n.Id == notifications[1].Id).IsRead);
        Assert.Equal(1, await GetUnreadCount(bob));

        // Idempotent: reading it again is still a 200 with an unchanged count.
        var again = await bob.PostAsync($"/api/v1/notifications/{notifications[0].Id}/read", content: null);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        var againCount = await again.Content.ReadFromJsonAsync<UnreadCountResponse>();
        Assert.Equal(1, againCount!.Count);
    }

    [Fact]
    public async Task SingleNotification_CanBeDeleted_AndUnreadCountDrops()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        await alice.PostAsync($"/api/v1/users/{bobName}/follow", content: null);
        var notification = Assert.Single((await GetNotifications(bob)).Notifications);
        Assert.Equal(1, await GetUnreadCount(bob));

        var delete = await bob.DeleteAsync($"/api/v1/notifications/{notification.Id}");
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);
        var count = await delete.Content.ReadFromJsonAsync<UnreadCountResponse>();
        Assert.Equal(0, count!.Count);

        Assert.Empty((await GetNotifications(bob)).Notifications);
        Assert.Equal(0, await GetUnreadCount(bob));

        // Deleting the same row again is a 404, not a second success.
        var again = await bob.DeleteAsync($"/api/v1/notifications/{notification.Id}");
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
    }

    [Fact]
    public async Task SingleNotification_Actions_AreScopedToTheRecipient()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        await alice.PostAsync($"/api/v1/users/{bobName}/follow", content: null);
        var notification = Assert.Single((await GetNotifications(bob)).Notifications);

        // Alice cannot read or delete Bob's notification; both look like missing rows.
        var read = await alice.PostAsync($"/api/v1/notifications/{notification.Id}/read", content: null);
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        var delete = await alice.DeleteAsync($"/api/v1/notifications/{notification.Id}");
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);

        // Bob's row is untouched.
        var stillUnread = Assert.Single((await GetNotifications(bob)).Notifications);
        Assert.False(stillUnread.IsRead);
        Assert.Equal(1, await GetUnreadCount(bob));
    }

    [Fact]
    public async Task Snooze_SuppressesEverything_ThenResumes()
    {
        var (alice, aliceName) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();

        // Past and over-24h windows are rejected, not stored.
        var past = await bob.PostAsJsonAsync(
            "/api/v1/users/me/snooze", new { until = DateTimeOffset.UtcNow.AddMinutes(-1) });
        Assert.Equal(HttpStatusCode.BadRequest, past.StatusCode);
        var far = await bob.PostAsJsonAsync(
            "/api/v1/users/me/snooze", new { until = DateTimeOffset.UtcNow.AddHours(25) });
        Assert.Equal(HttpStatusCode.BadRequest, far.StatusCode);

        var snooze = await bob.PostAsJsonAsync(
            "/api/v1/users/me/snooze", new { until = DateTimeOffset.UtcNow.AddHours(4) });
        Assert.Equal(HttpStatusCode.OK, snooze.StatusCode);

        await alice.PostAsync($"/api/v1/users/{bobName}/follow", content: null);
        Assert.Empty((await GetNotifications(bob)).Notifications);
        Assert.Equal(0, await GetUnreadCount(bob));

        var lift = await bob.DeleteAsync("/api/v1/users/me/snooze");
        Assert.Equal(HttpStatusCode.NoContent, lift.StatusCode);

        // A fresh event after lifting notifies (the snoozed follow stays buried).
        var (cara, caraName) = await RegisterClient();
        await cara.PostAsync($"/api/v1/users/{bobName}/follow", content: null);
        var notification = Assert.Single((await GetNotifications(bob)).Notifications);
        Assert.Equal(caraName, notification.Actor.Username);
    }

    [Fact]
    public async Task Snooze_StopsSuppressing_WhenItsWindowExpires()
    {
        var (bob, bobName) = await RegisterClient();
        // Register the future actor BEFORE moving the clock: her bearer token must be issued at
        // real time, or its `nbf` would sit in the future once the clock snaps back.
        var (cara, caraName) = await RegisterClient();

        var until = factory.Clock.UtcNow.AddHours(1);
        var snooze = await bob.PostAsJsonAsync("/api/v1/users/me/snooze", new { until });
        Assert.Equal(HttpStatusCode.OK, snooze.StatusCode);

        try
        {
            // Past the snooze instant — no explicit lift, no second client action.
            factory.Clock.Set(until.AddMinutes(1));
            await cara.PostAsync($"/api/v1/users/{bobName}/follow", content: null);

            var notification = Assert.Single((await GetNotifications(bob)).Notifications);
            Assert.Equal(caraName, notification.Actor.Username);
            Assert.Equal(1, await GetUnreadCount(bob));
        }
        finally
        {
            factory.Clock.Reset();
        }
    }

    [Fact]
    public async Task UnreadCount_TracksAndClearsOnRead()
    {
        var (author, _) = await RegisterClient();
        var (a, _) = await RegisterClient();
        var (b, _) = await RegisterClient();
        var postId = await CreatePost(author, "popular");

        await a.PostAsync($"/api/v1/posts/{postId}/like", content: null);
        await b.PostAsync($"/api/v1/posts/{postId}/like", content: null);

        Assert.Equal(2, await GetUnreadCount(author));

        var read = await author.PostAsync("/api/v1/notifications/read", content: null);
        Assert.Equal(HttpStatusCode.NoContent, read.StatusCode);

        Assert.Equal(0, await GetUnreadCount(author));
        // The notifications still exist, just marked read.
        Assert.All((await GetNotifications(author)).Notifications, n => Assert.True(n.IsRead));
    }

    [Fact]
    public async Task RedundantFollow_DoesNotDoubleNotify()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();

        // Following someone you already follow is an idempotent no-op — it must not fire a
        // second notification.
        await alice.PostAsync($"/api/v1/users/{bobName}/follow", content: null);
        await alice.PostAsync($"/api/v1/users/{bobName}/follow", content: null);

        var notification = Assert.Single((await GetNotifications(bob)).Notifications);
        Assert.Equal("NewFollower", notification.Type);
    }

    [Fact]
    public async Task Notifications_RequireAuth()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/notifications");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ResolvedReport_NotifiesOnlyTheReporter_WithReportAsEntity()
    {
        var (author, _) = await RegisterClient();
        var (reporter, _) = await RegisterClient();
        var postId = await CreatePost(author, "reported post");

        await reporter.PostAsJsonAsync($"/api/v1/posts/{postId}/report", new { category = "Spam" });

        var moderator = await ModeratorClient();
        var queue = await moderator.GetFromJsonAsync<ReportQueueResponse>("/api/v1/admin/reports");
        var report = Assert.Single(queue!.Reports, r => r.TargetId == postId);

        var resolved = await moderator.PostAsJsonAsync(
            $"/api/v1/admin/reports/{report.Id}/resolve",
            new { action = "Dismiss", reason = "no violation" });
        Assert.Equal(HttpStatusCode.NoContent, resolved.StatusCode);

        var notification = Assert.Single((await GetNotifications(reporter)).Notifications);
        Assert.Equal("ReportResolved", notification.Type);
        Assert.Equal(report.Id, notification.EntityId);

        // The reported author is never told who reported them, nor that a report existed.
        Assert.Empty((await GetNotifications(author)).Notifications);
    }

    [Fact]
    public async Task DirectModerationAction_NotifiesTheAffectedUser()
    {
        var moderator = await ModeratorClient();
        var (user, username) = await RegisterClient();

        var warn = await moderator.PostAsJsonAsync(
            $"/api/v1/admin/users/{username}/action",
            new { action = "Warning", reason = "be kinder" });
        Assert.Equal(HttpStatusCode.NoContent, warn.StatusCode);

        var notification = Assert.Single((await GetNotifications(user)).Notifications);
        Assert.Equal("ModerationAction", notification.Type);
        Assert.NotEmpty(notification.EntityId);
    }
}
