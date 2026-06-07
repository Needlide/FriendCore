using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FriendCore.Application.Common.Models;
using FriendCore.IntegrationTests.Infrastructure;

namespace FriendCore.IntegrationTests.Posts;

public class HomeFeedFlowTests(FriendCoreApiFactory factory) : IClassFixture<FriendCoreApiFactory>
{
    private async Task<(HttpClient Client, string Username)> AuthedClient()
    {
        factory.Clock.Reset();
        var client = factory.CreateClient();
        var username = AuthScenarios.NewUsername();
        var email = AuthScenarios.UniqueEmail();
        await AuthScenarios.Register(client, email, username);
        var login = await AuthScenarios.Login(client, email);
        var body = await login.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        return (client, username);
    }

    private static async Task<string> CreatePost(HttpClient client, string content)
    {
        var response = await client.PostAsJsonAsync("/api/v1/posts", new { content, sensitivity = "General" });
        var body = await response.Content.ReadFromJsonAsync<PostResponse>();
        return body!.Id;
    }

    [Fact]
    public async Task Feed_ShowsOnlyFollowedAuthorsPosts_NewestFirst()
    {
        var (viewer, _) = await AuthedClient();
        var (followed, followedName) = await AuthedClient();
        var (stranger, _) = await AuthedClient();

        await viewer.PostAsync($"/api/v1/users/{followedName}/follow", content: null);

        await CreatePost(followed, "followed first");
        await CreatePost(followed, "followed second");
        await CreatePost(stranger, "stranger post");
        await CreatePost(viewer, "my own post");

        var feed = await viewer.GetFromJsonAsync<PostFeedResponse>("/api/v1/feed");

        Assert.Equal(2, feed!.Posts.Count);
        Assert.Equal("followed second", feed.Posts[0].Content);
        Assert.Equal("followed first", feed.Posts[1].Content);
        Assert.DoesNotContain(feed.Posts, p => p.Content == "stranger post");
        Assert.DoesNotContain(feed.Posts, p => p.Content == "my own post");
    }

    [Fact]
    public async Task Feed_ReflectsLikedFlag()
    {
        var (viewer, _) = await AuthedClient();
        var (followed, followedName) = await AuthedClient();
        await viewer.PostAsync($"/api/v1/users/{followedName}/follow", content: null);
        var postId = await CreatePost(followed, "like me");

        await viewer.PostAsync($"/api/v1/posts/{postId}/like", content: null);

        var feed = await viewer.GetFromJsonAsync<PostFeedResponse>("/api/v1/feed");

        var item = Assert.Single(feed!.Posts);
        Assert.True(item.IsLikedByCurrentUser);
        Assert.Equal(1, item.LikeCount);
    }

    [Fact]
    public async Task Feed_RequiresAuthentication()
    {
        factory.Clock.Reset();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/feed");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Feed_IsEmpty_WhenNotFollowingAnyone()
    {
        var (viewer, _) = await AuthedClient();
        var (other, _) = await AuthedClient();
        await CreatePost(other, "nobody follows me");

        var feed = await viewer.GetFromJsonAsync<PostFeedResponse>("/api/v1/feed");

        Assert.Empty(feed!.Posts);
    }
}
