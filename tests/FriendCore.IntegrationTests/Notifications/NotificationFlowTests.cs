using System.Net;
using System.Net.Http.Json;
using FriendCore.Application.Common.Models;
using FriendCore.IntegrationTests.Infrastructure;

namespace FriendCore.IntegrationTests.Notifications;

/// <summary>
///     Spec §5.7/§11/§17.2: the four notification types fire from their triggering actions, the
///     bell counts unread and clears on read, and self-actions never notify.
/// </summary>
public class NotificationFlowTests(FriendCoreApiFactory factory) : IClassFixture<FriendCoreApiFactory>
{
    private Task<(HttpClient Client, string Username)> RegisterClient() =>
        new AuthScenarios(factory).RegisterVerifiedClientAsync();

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
    public async Task SelfLike_DoesNotNotify()
    {
        var (author, _) = await RegisterClient();
        var postId = await CreatePost(author, "my own post");

        await author.PostAsync($"/api/v1/posts/{postId}/like", content: null);

        Assert.Empty((await GetNotifications(author)).Notifications);
        Assert.Equal(0, await GetUnreadCount(author));
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
}
