using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FriendCore.Application.Common.Models;
using FriendCore.IntegrationTests.Infrastructure;

namespace FriendCore.IntegrationTests.Social;

/// <summary>
///     Spec §13.6: mute hides content without blocking. One-way, feed-only — the assertions on
///     what STAYS visible matter as much as what disappears (mute must not behave like a block).
/// </summary>
public class MuteFlowTests(FriendCoreApiFactory factory) : IClassFixture<FriendCoreApiFactory>
{
    private async Task<(HttpClient Client, string Username)> RegisterClient()
    {
        factory.Clock.Reset();
        var client = factory.CreateClient();
        var email = AuthScenarios.UniqueEmail();
        var username = AuthScenarios.NewUsername();
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
    public async Task Mute_RemovesPostsFromHomeFeed_ButNothingElse()
    {
        var (aClient, _) = await RegisterClient();
        var (bClient, bUsername) = await RegisterClient();
        await aClient.PostAsync($"/api/v1/users/{bUsername}/follow", content: null);
        var bPostId = await CreatePost(bClient, "muted away");

        var feedBefore = await aClient.GetFromJsonAsync<PostFeedResponse>("/api/v1/feed");
        Assert.Contains(feedBefore!.Posts, p => p.Id == bPostId);

        var mute = await aClient.PostAsync($"/api/v1/users/{bUsername}/mute", content: null);
        Assert.Equal(HttpStatusCode.NoContent, mute.StatusCode);

        // Gone from the feed…
        var feedAfter = await aClient.GetFromJsonAsync<PostFeedResponse>("/api/v1/feed");
        Assert.DoesNotContain(feedAfter!.Posts, p => p.Id == bPostId);

        // …but mute is NOT a block: profile, direct post access, and the user's post listing
        // all stay reachable, and the follow relationship is untouched.
        var profile = await aClient.GetAsync($"/api/v1/users/{bUsername}");
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
        var directPost = await aClient.GetAsync($"/api/v1/posts/{bPostId}");
        Assert.Equal(HttpStatusCode.OK, directPost.StatusCode);
        // The muted post itself is still listed when A deliberately visits B's profile posts.
        var userPosts = await aClient.GetFromJsonAsync<PostFeedResponse>($"/api/v1/users/{bUsername}/posts");
        Assert.Contains(userPosts!.Posts, p => p.Id == bPostId);
    }

    [Fact]
    public async Task Mute_IsOneWay_MutedUserStillSeesMuter()
    {
        var (aClient, aUsername) = await RegisterClient();
        var (bClient, bUsername) = await RegisterClient();
        await bClient.PostAsync($"/api/v1/users/{aUsername}/follow", content: null);
        var aPostId = await CreatePost(aClient, "still visible to b");

        await aClient.PostAsync($"/api/v1/users/{bUsername}/mute", content: null);

        // B (the muted one) is unaffected and unaware: A's posts stay in B's feed.
        var bFeed = await bClient.GetFromJsonAsync<PostFeedResponse>("/api/v1/feed");
        Assert.Contains(bFeed!.Posts, p => p.Id == aPostId);
        var aProfile = await bClient.GetAsync($"/api/v1/users/{aUsername}");
        Assert.Equal(HttpStatusCode.OK, aProfile.StatusCode);
    }

    [Fact]
    public async Task Mute_DoesNotTouchFollowRelationship()
    {
        var (aClient, aUsername) = await RegisterClient();
        var (_, bUsername) = await RegisterClient();
        await aClient.PostAsync($"/api/v1/users/{bUsername}/follow", content: null);

        await aClient.PostAsync($"/api/v1/users/{bUsername}/mute", content: null);

        var following = await aClient.GetFromJsonAsync<FollowListResponse>($"/api/v1/users/{aUsername}/following");
        Assert.Contains(following!.Users, u => u.Username == bUsername);
    }

    [Fact]
    public async Task Unmute_RestoresTheFeed()
    {
        var (aClient, _) = await RegisterClient();
        var (bClient, bUsername) = await RegisterClient();
        await aClient.PostAsync($"/api/v1/users/{bUsername}/follow", content: null);
        var bPostId = await CreatePost(bClient, "back again");
        await aClient.PostAsync($"/api/v1/users/{bUsername}/mute", content: null);

        var unmute = await aClient.DeleteAsync($"/api/v1/users/{bUsername}/mute");
        Assert.Equal(HttpStatusCode.NoContent, unmute.StatusCode);

        var feed = await aClient.GetFromJsonAsync<PostFeedResponse>("/api/v1/feed");
        Assert.Contains(feed!.Posts, p => p.Id == bPostId);
    }

    [Fact]
    public async Task Mute_IsIdempotent()
    {
        var (aClient, _) = await RegisterClient();
        var (_, bUsername) = await RegisterClient();

        var first = await aClient.PostAsync($"/api/v1/users/{bUsername}/mute", content: null);
        var second = await aClient.PostAsync($"/api/v1/users/{bUsername}/mute", content: null);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
    }

    [Fact]
    public async Task Mute_Self_ReturnsBadRequest()
    {
        var (client, username) = await RegisterClient();

        var response = await client.PostAsync($"/api/v1/users/{username}/mute", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Mute_UnknownUser_ReturnsNotFound()
    {
        var (client, _) = await RegisterClient();

        var response = await client.PostAsync($"/api/v1/users/{AuthScenarios.NewUsername()}/mute", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Mute_AcrossABlock_ReturnsNotFound()
    {
        // Blocks already hide everything; the target "does not exist" for mute purposes too.
        var (aClient, _) = await RegisterClient();
        var (_, bUsername) = await RegisterClient();
        await aClient.PostAsync($"/api/v1/users/{bUsername}/block", content: null);

        var response = await aClient.PostAsync($"/api/v1/users/{bUsername}/mute", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
