using System.Net;
using System.Net.Http.Json;
using FriendCore.Application.Common.Models;
using FriendCore.IntegrationTests.Infrastructure;

namespace FriendCore.IntegrationTests.Social;

/// <summary>
///     §9.1: explicit/sensitive content filtered by the viewer's ContentPreference, plus the
///     §17.2 PATCH /users/me/preferences endpoint that controls it. Default is General — the
///     safe floor; users opt IN to heavier content.
/// </summary>
public class ContentPreferenceFlowTests(FriendCoreApiFactory factory) : IClassFixture<FriendCoreApiFactory>
{
    private Task<(HttpClient Client, string Username)> RegisterClient() =>
        new AuthScenarios(factory).RegisterVerifiedClientAsync();

    private static async Task<string> CreatePost(HttpClient client, string content, string sensitivity)
    {
        var response = await client.PostAsJsonAsync("/api/v1/posts", new { content, sensitivity });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PostResponse>();
        return body!.Id;
    }

    private static Task<HttpResponseMessage> SetContentPreference(HttpClient client, string preference)
    {
        return client.PatchAsJsonAsync("/api/v1/users/me/preferences", new
        {
            hideFlashingMedia = true,
            reduceMotion = false,
            disableAutoplay = true,
            reduceVisualNoise = false,
            contentPreference = preference
        });
    }

    [Fact]
    public async Task UpdatePreferences_RoundTripsAllFields()
    {
        var (client, _) = await RegisterClient();

        var response = await SetContentPreference(client, "Explicit");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PreferencesResponse>();
        Assert.Equal("Explicit", body!.ContentPreference);
        Assert.True(body.HideFlashingMedia);
        Assert.False(body.ReduceMotion);
    }

    [Fact]
    public async Task Feed_ShowsOnlyContentUpToTheViewersPreference()
    {
        var (author, authorName) = await RegisterClient();
        var (viewer, _) = await RegisterClient();
        await viewer.PostAsync($"/api/v1/users/{authorName}/follow", content: null);
        var general = await CreatePost(author, "general post", "General");
        var sensitive = await CreatePost(author, "sensitive post", "Sensitive");
        var explicitPost = await CreatePost(author, "explicit post", "Explicit");

        // Fresh accounts default to General — the safe floor.
        var defaultFeed = (await viewer.GetFromJsonAsync<PostFeedResponse>("/api/v1/feed"))!.Posts;
        Assert.Equal([general], defaultFeed.Select(p => p.Id).ToArray());

        await SetContentPreference(viewer, "Sensitive");
        var sensitiveFeed = (await viewer.GetFromJsonAsync<PostFeedResponse>("/api/v1/feed"))!.Posts;
        Assert.Equal([sensitive, general], sensitiveFeed.Select(p => p.Id).ToArray());

        await SetContentPreference(viewer, "Explicit");
        var explicitFeed = (await viewer.GetFromJsonAsync<PostFeedResponse>("/api/v1/feed"))!.Posts;
        Assert.Equal([explicitPost, sensitive, general], explicitFeed.Select(p => p.Id).ToArray());
    }

    [Fact]
    public async Task Feed_FilteringDoesNotAffectDirectPostAccess()
    {
        var (author, authorName) = await RegisterClient();
        var (viewer, _) = await RegisterClient();
        await viewer.PostAsync($"/api/v1/users/{authorName}/follow", content: null);
        var explicitPost = await CreatePost(author, "explicit post", "Explicit");

        // Deliberate navigation stays open (same boundary as mute/keyword filters); the client
        // uses the Sensitivity field on the response to warn/blur.
        var direct = await viewer.GetFromJsonAsync<PostResponse>($"/api/v1/posts/{explicitPost}");
        Assert.Equal("Explicit", direct!.Sensitivity);
    }

    [Fact]
    public async Task DiscoveryPosts_RespectContentPreference()
    {
        var tag = $"tag-{Ulid.NewUlid().ToString().ToLowerInvariant()}";
        var (viewer, _) = await RegisterClient();
        await viewer.PutAsJsonAsync(
            "/api/v1/users/me/tags",
            new { tags = new[] { new { name = tag, tier = "Primary", isPublic = true } } });

        var (author, _) = await RegisterClient();
        var response = await author.PostAsJsonAsync(
            "/api/v1/posts", new { content = "explicit tagged", sensitivity = "Explicit", tags = new[] { tag } });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var keptResponse = await author.PostAsJsonAsync(
            "/api/v1/posts", new { content = "general tagged", sensitivity = "General", tags = new[] { tag } });
        var kept = await keptResponse.Content.ReadFromJsonAsync<PostResponse>();

        var posts = (await viewer.GetFromJsonAsync<DiscoveryPostsResponse>("/api/v1/feed/discovery/posts"))!.Posts;

        Assert.Equal(kept!.Id, Assert.Single(posts).Id);
    }

    [Fact]
    public async Task UpdatePreferences_RequiresAuth()
    {
        var response = await SetContentPreference(factory.CreateClient(), "Explicit");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
