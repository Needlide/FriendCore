using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FriendCore.Application.Common.Models;
using FriendCore.IntegrationTests.Infrastructure;

namespace FriendCore.IntegrationTests.Discovery;

/// <summary>
///     Spec §9.2 posts tab: posts carrying tags that overlap the viewer's profile tags, from
///     users not currently followed; mute/keyword/block exclusions apply like everywhere else.
/// </summary>
public class DiscoveryPostsFlowTests(FriendCoreApiFactory factory) : IClassFixture<FriendCoreApiFactory>
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

    private static async Task SetProfileTags(HttpClient client, params string[] tagNames)
    {
        var response = await client.PutAsJsonAsync(
            "/api/v1/users/me/tags",
            new { tags = tagNames.Select(name => new { name, tier = "Primary", isPublic = true }).ToArray() });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<string> CreateTaggedPost(HttpClient client, string content, params string[] tags)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/posts", new { content, sensitivity = "General", tags });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PostResponse>();
        return body!.Id;
    }

    private static string UniqueTag() => $"tag-{Ulid.NewUlid().ToString().ToLowerInvariant()}";

    private static async Task<DiscoveryPostsResponse> GetPosts(HttpClient client)
    {
        return (await client.GetFromJsonAsync<DiscoveryPostsResponse>("/api/v1/feed/discovery/posts"))!;
    }

    [Fact]
    public async Task Posts_ShowsTagOverlappingPosts_FromUnfollowedAuthors_NewestFirst()
    {
        var tag = UniqueTag();
        var (viewer, _) = await RegisterClient();
        await SetProfileTags(viewer, tag);
        var (author, _) = await RegisterClient();

        var older = await CreateTaggedPost(author, "older", tag);
        var newer = await CreateTaggedPost(author, "newer", tag);
        await CreateTaggedPost(author, "unrelated", UniqueTag());

        var posts = (await GetPosts(viewer)).Posts;

        Assert.Equal([newer, older], posts.Select(p => p.Id).ToArray());
    }

    [Fact]
    public async Task Posts_ExcludesOwnPosts_AndFollowedAuthors()
    {
        var tag = UniqueTag();
        var (viewer, _) = await RegisterClient();
        await SetProfileTags(viewer, tag);
        await CreateTaggedPost(viewer, "my own", tag);

        var (followed, followedName) = await RegisterClient();
        await viewer.PostAsync($"/api/v1/users/{followedName}/follow", content: null);
        await CreateTaggedPost(followed, "from someone I follow", tag);

        var posts = (await GetPosts(viewer)).Posts;

        Assert.Empty(posts);
    }

    [Fact]
    public async Task Posts_ExcludesMutedAndBlockedAuthors()
    {
        var tag = UniqueTag();
        var (viewer, _) = await RegisterClient();
        await SetProfileTags(viewer, tag);

        var (muted, mutedName) = await RegisterClient();
        await CreateTaggedPost(muted, "from muted", tag);
        var (blocked, blockedName) = await RegisterClient();
        await CreateTaggedPost(blocked, "from blocked", tag);
        var (clean, _) = await RegisterClient();
        var keptId = await CreateTaggedPost(clean, "kept", tag);

        await viewer.PostAsync($"/api/v1/users/{mutedName}/mute", content: null);
        await viewer.PostAsync($"/api/v1/users/{blockedName}/block", content: null);

        var posts = (await GetPosts(viewer)).Posts;

        Assert.Equal(keptId, Assert.Single(posts).Id);
    }

    [Fact]
    public async Task Posts_RespectKeywordFilters()
    {
        var tag = UniqueTag();
        var (viewer, _) = await RegisterClient();
        await SetProfileTags(viewer, tag);
        await viewer.PatchAsJsonAsync("/api/v1/users/me/keyword-filters", new { keywords = new[] { "wasps" } });

        var (author, _) = await RegisterClient();
        await CreateTaggedPost(author, "WASPS everywhere", tag);
        var keptId = await CreateTaggedPost(author, "calm content", tag);

        var posts = (await GetPosts(viewer)).Posts;

        Assert.Equal(keptId, Assert.Single(posts).Id);
    }

    [Fact]
    public async Task Posts_EmptyForViewerWithoutProfileTags()
    {
        var (viewer, _) = await RegisterClient();

        var posts = (await GetPosts(viewer)).Posts;

        Assert.Empty(posts);
    }

    [Fact]
    public async Task CreatePost_WithTooManyTags_ReturnsBadRequest()
    {
        var (client, _) = await RegisterClient();
        var tags = Enumerable.Range(0, 13).Select(_ => UniqueTag()).ToArray();

        var response = await client.PostAsJsonAsync(
            "/api/v1/posts", new { content = "over-tagged", sensitivity = "General", tags });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Post.TooManyTags", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task CreatePost_WithDuplicateTags_ReturnsBadRequest()
    {
        var (client, _) = await RegisterClient();
        var tag = UniqueTag();

        var response = await client.PostAsJsonAsync(
            "/api/v1/posts",
            new { content = "duplicated", sensitivity = "General", tags = new[] { tag, tag.ToUpperInvariant() } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Posts_RequiresAuth()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/feed/discovery/posts");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
