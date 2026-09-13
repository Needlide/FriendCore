using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FriendCore.Application.Common.Models;
using FriendCore.IntegrationTests.Infrastructure;

namespace FriendCore.IntegrationTests.Posts;

public class HomeFeedFlowTests(FriendCoreApiFactory factory) : IClassFixture<FriendCoreApiFactory>
{
    private Task<(HttpClient Client, string Username)> AuthedClient() =>
        new AuthScenarios(factory).RegisterVerifiedClientAsync();

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

        await viewer.PostAsync($"/api/v1/users/{followedName}/follow", null);

        await CreatePost(followed, "followed first");
        await CreatePost(followed, "followed second");
        await CreatePost(stranger, "stranger post");
        await CreatePost(viewer, "my own post");

        var feed = await viewer.GetFromJsonAsync<PostFeedResponse>("/api/v1/feed/home");

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
        await viewer.PostAsync($"/api/v1/users/{followedName}/follow", null);
        var postId = await CreatePost(followed, "like me");

        await viewer.PostAsync($"/api/v1/posts/{postId}/like", null);

        var feed = await viewer.GetFromJsonAsync<PostFeedResponse>("/api/v1/feed/home");

        var item = Assert.Single(feed!.Posts);
        Assert.True(item.IsLikedByCurrentUser);
        Assert.Equal(1, item.LikeCount);
    }

    [Fact]
    public async Task Feed_RequiresAuthentication()
    {
        factory.Clock.Reset();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/feed/home");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Feed_IsEmpty_WhenNotFollowingAnyone()
    {
        var (viewer, _) = await AuthedClient();
        var (other, _) = await AuthedClient();
        await CreatePost(other, "nobody follows me");

        var feed = await viewer.GetFromJsonAsync<PostFeedResponse>("/api/v1/feed/home");

        Assert.Empty(feed!.Posts);
    }

    [Fact]
    public async Task Feed_PaginatesAcrossTwoPages_WithoutOverlapOrGaps()
    {
        var (viewer, _) = await AuthedClient();
        var (followed, followedName) = await AuthedClient();
        await viewer.PostAsync($"/api/v1/users/{followedName}/follow", null);

        // 25 posts > PageSize (20). Sequential awaited POSTs get distinct, increasing CreatedAt,
        // so "post-24" is newest. Expected feed order is strictly descending: 24, 23, ... , 0.
        const int total = 25;
        for (var i = 0; i < total; i++)
            await CreatePost(followed, $"post-{i:D2}");

        var page1 = await viewer.GetFromJsonAsync<PostFeedResponse>("/api/v1/feed/home");
        Assert.Equal(20, page1!.Posts.Count);
        Assert.NotNull(page1.NextCursor);

        var page2 = await viewer.GetFromJsonAsync<PostFeedResponse>(
            $"/api/v1/feed/home?cursor={Uri.EscapeDataString(page1.NextCursor!)}");
        Assert.Equal(5, page2!.Posts.Count);
        Assert.Null(page2.NextCursor);

        // Concatenating the pages must reproduce every post exactly once, in descending order —
        // this single check catches overlaps (duplicates), gaps (missing), and ordering errors.
        var seen = page1.Posts.Concat(page2.Posts).Select(p => p.Content).OfType<string>().ToList();
        var expected = Enumerable.Range(0, total).Reverse().Select(i => $"post-{i:D2}").ToList();
        Assert.Equal(expected, seen);
        Assert.Equal(total, seen.Distinct().Count());
    }

    [Fact]
    public async Task Feed_PaginatesAcrossTiedTimestamps_ViaIdTiebreaker()
    {
        var scenarios = new AuthScenarios(factory);
        var (viewer, _) = await AuthedClient();

        var authorClient = factory.CreateClient();
        var authorEmail = AuthScenarios.UniqueEmail();
        var authorName = AuthScenarios.NewUsername();
        await AuthScenarios.Register(authorClient, authorEmail, authorName);
        var authorId = await scenarios.GetUserIdAsync(authorEmail);
        await viewer.PostAsync($"/api/v1/users/{authorName}/follow", null);

        // All 25 posts share one CreatedAt. A CreatedAt-only cursor would page on `CreatedAt < X`,
        // which is false for every tied row at the boundary — silently dropping page 2 entirely.
        // The Id tiebreaker is the only thing that makes the boundary advance.
        const int total = 25;
        var tied = DateTimeOffset.UtcNow.AddMinutes(-5);
        var ids = await scenarios.SeedPostsWithSameTimestampAsync(authorId, total, tied);

        var page1 = await viewer.GetFromJsonAsync<PostFeedResponse>("/api/v1/feed/home");
        Assert.Equal(20, page1!.Posts.Count);
        Assert.NotNull(page1.NextCursor);

        var page2 = await viewer.GetFromJsonAsync<PostFeedResponse>(
            $"/api/v1/feed/home?cursor={Uri.EscapeDataString(page1.NextCursor!)}");
        Assert.Equal(5, page2!.Posts.Count);
        Assert.Null(page2.NextCursor);

        // With timestamps tied, the feed must fall back to ordering by Id descending, and the two
        // pages together must still be every seeded post exactly once.
        var returned = page1.Posts.Concat(page2.Posts).Select(p => Ulid.Parse(p.Id)).ToList();
        var expected = ids.OrderByDescending(id => id).ToList();
        Assert.Equal(expected, returned);
    }
}