using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FriendCore.Application.Common.Models;
using FriendCore.IntegrationTests.Infrastructure;
using FriendCore.IntegrationTests.Media;

namespace FriendCore.IntegrationTests.Social;

/// <summary>
///     Spec §13.6 keyword filter: "never see posts containing specific words" — applied to the
///     home feed query (and discovery later), not to deliberate navigation.
/// </summary>
public class KeywordFilterFlowTests(FriendCoreApiFactory factory) : IClassFixture<FriendCoreApiFactory>
{
    private Task<(HttpClient Client, string Username)> RegisterClient() =>
        new AuthScenarios(factory).RegisterVerifiedClientAsync();

    private static async Task<string> CreatePost(HttpClient client, string? content, string? mediaId = null)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/posts",
            new { content, sensitivity = "General", mediaIds = mediaId is null ? [] : new[] { mediaId } });
        var body = await response.Content.ReadFromJsonAsync<PostResponse>();
        return body!.Id;
    }

    private static async Task<string> UploadReadyMedia(HttpClient client)
    {
        var file = new ByteArrayContent(TestImages.RealPng(60, 40));
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        var form = new MultipartFormDataContent { { file, "file", "image.png" } };

        var response = await client.PostAsync("/api/v1/media", form);
        var body = await response.Content.ReadFromJsonAsync<MediaStatusResponse>();
        Assert.Equal("Ready", await MediaUploadFlowTests.PollStatusAsync(client, body!.Id));
        return body.Id;
    }

    [Fact]
    public async Task PatchAndGet_RoundTripsTheNormalizedSet()
    {
        var (client, _) = await RegisterClient();

        var patch = await client.PatchAsJsonAsync(
            "/api/v1/users/me/keyword-filters",
            new { keywords = new[] { "  Spoilers ", "SPOILERS", "wasps" } });

        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        var patched = await patch.Content.ReadFromJsonAsync<KeywordFiltersResponse>();
        Assert.Equal(["spoilers", "wasps"], patched!.Keywords);

        var fetched = await client.GetFromJsonAsync<KeywordFiltersResponse>("/api/v1/users/me/keyword-filters");
        Assert.Equal(["spoilers", "wasps"], fetched!.Keywords);
    }

    [Fact]
    public async Task Feed_HidesMatchingPosts_CaseInsensitiveSubstring_ButNotDirectAccess()
    {
        var (aClient, _) = await RegisterClient();
        var (bClient, bUsername) = await RegisterClient();
        await aClient.PostAsync($"/api/v1/users/{bUsername}/follow", content: null);
        var matching = await CreatePost(bClient, "Major WASPS sighting downtown!");
        var unrelated = await CreatePost(bClient, "lovely weather today");

        await aClient.PatchAsJsonAsync("/api/v1/users/me/keyword-filters", new { keywords = new[] { "wasps" } });

        var feed = await aClient.GetFromJsonAsync<PostFeedResponse>("/api/v1/feed/home");
        Assert.DoesNotContain(feed!.Posts, p => p.Id == matching);
        Assert.Contains(feed.Posts, p => p.Id == unrelated);

        // Like mute, the filter applies to the feed, not to deliberate navigation: the post is
        // still reachable by id and on the author's profile listing.
        var direct = await aClient.GetAsync($"/api/v1/posts/{matching}");
        Assert.Equal(HttpStatusCode.OK, direct.StatusCode);
        var profilePosts = await aClient.GetFromJsonAsync<PostFeedResponse>($"/api/v1/users/{bUsername}/posts");
        Assert.Contains(profilePosts!.Posts, p => p.Id == matching);
    }

    [Fact]
    public async Task Feed_NeverHidesMediaOnlyPosts()
    {
        var (aClient, _) = await RegisterClient();
        var (bClient, bUsername) = await RegisterClient();
        await aClient.PostAsync($"/api/v1/users/{bUsername}/follow", content: null);
        var mediaId = await UploadReadyMedia(bClient);
        var mediaOnly = await CreatePost(bClient, content: null, mediaId);

        await aClient.PatchAsJsonAsync("/api/v1/users/me/keyword-filters", new { keywords = new[] { "wasps" } });

        // Null content can never match a keyword — the SQL path must not drop it.
        var feed = await aClient.GetFromJsonAsync<PostFeedResponse>("/api/v1/feed/home");
        Assert.Contains(feed!.Posts, p => p.Id == mediaOnly);
    }

    [Fact]
    public async Task ClearingFilters_RestoresTheFeed()
    {
        var (aClient, _) = await RegisterClient();
        var (bClient, bUsername) = await RegisterClient();
        await aClient.PostAsync($"/api/v1/users/{bUsername}/follow", content: null);
        var postId = await CreatePost(bClient, "wasps everywhere");
        await aClient.PatchAsJsonAsync("/api/v1/users/me/keyword-filters", new { keywords = new[] { "wasps" } });

        var cleared = await aClient.PatchAsJsonAsync(
            "/api/v1/users/me/keyword-filters", new { keywords = Array.Empty<string>() });
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);

        var feed = await aClient.GetFromJsonAsync<PostFeedResponse>("/api/v1/feed/home");
        Assert.Contains(feed!.Posts, p => p.Id == postId);
    }

    [Fact]
    public async Task Patch_TooManyFilters_ReturnsBadRequest()
    {
        var (client, _) = await RegisterClient();
        var keywords = Enumerable.Range(0, 51).Select(i => $"keyword-{i}").ToArray();

        var response = await client.PatchAsJsonAsync("/api/v1/users/me/keyword-filters", new { keywords });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("User.TooManyKeywordFilters", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Patch_OverlongFilter_ReturnsBadRequest()
    {
        var (client, _) = await RegisterClient();

        var response = await client.PatchAsJsonAsync(
            "/api/v1/users/me/keyword-filters", new { keywords = new[] { new string('x', 101) } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("User.KeywordFilterTooLong", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Get_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/users/me/keyword-filters");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
