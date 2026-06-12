using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FriendCore.Application.Common.Models;
using FriendCore.IntegrationTests.Infrastructure;

namespace FriendCore.IntegrationTests.Social;

/// <summary>
///     Spec §8.2: the global query filter hides blocked users' content everywhere —
///     these flows never check blocks in their handlers, the DbContext does it.
/// </summary>
public class BlockVisibilityTests(FriendCoreApiFactory factory) : IClassFixture<FriendCoreApiFactory>
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

    private static async Task<string> CreatePost(HttpClient client, string content = "a post")
    {
        var response = await client.PostAsJsonAsync("/api/v1/posts", new { content, sensitivity = "General" });
        var body = await response.Content.ReadFromJsonAsync<PostResponse>();
        return body!.Id;
    }

    [Fact]
    public async Task Block_HidesCommentsFromEachOther_OnAThirdPartysPost()
    {
        var (aClient, _) = await RegisterClient();
        var (bClient, bUsername) = await RegisterClient();
        var (cClient, _) = await RegisterClient();
        var postId = await CreatePost(cClient, "neutral ground");
        await aClient.PostAsJsonAsync($"/api/v1/posts/{postId}/comments", new { content = "from a" });
        await bClient.PostAsJsonAsync($"/api/v1/posts/{postId}/comments", new { content = "from b" });

        await aClient.PostAsync($"/api/v1/users/{bUsername}/block", content: null);

        // Each side of the block loses the other's comment; their own stays.
        var seenByA = await aClient.GetFromJsonAsync<CommentFeedResponse>($"/api/v1/posts/{postId}/comments");
        Assert.Single(seenByA!.Comments);
        Assert.Equal("from a", seenByA.Comments[0].Content);

        var seenByB = await bClient.GetFromJsonAsync<CommentFeedResponse>($"/api/v1/posts/{postId}/comments");
        Assert.Single(seenByB!.Comments);
        Assert.Equal("from b", seenByB.Comments[0].Content);

        // The uninvolved author still sees both.
        var seenByC = await cClient.GetFromJsonAsync<CommentFeedResponse>($"/api/v1/posts/{postId}/comments");
        Assert.Equal(2, seenByC!.Comments.Count);
    }

    [Fact]
    public async Task Block_HidesPostsById_BothWays()
    {
        var (aClient, _) = await RegisterClient();
        var (bClient, bUsername) = await RegisterClient();
        var aPostId = await CreatePost(aClient, "by a");
        var bPostId = await CreatePost(bClient, "by b");

        await aClient.PostAsync($"/api/v1/users/{bUsername}/block", content: null);

        var bPostSeenByA = await aClient.GetAsync($"/api/v1/posts/{bPostId}");
        Assert.Equal(HttpStatusCode.NotFound, bPostSeenByA.StatusCode);
        var aPostSeenByB = await bClient.GetAsync($"/api/v1/posts/{aPostId}");
        Assert.Equal(HttpStatusCode.NotFound, aPostSeenByB.StatusCode);

        // Each still sees their own post, and anonymous visitors see both.
        var ownPost = await bClient.GetAsync($"/api/v1/posts/{bPostId}");
        Assert.Equal(HttpStatusCode.OK, ownPost.StatusCode);
        var anonymous = await factory.CreateClient().GetAsync($"/api/v1/posts/{bPostId}");
        Assert.Equal(HttpStatusCode.OK, anonymous.StatusCode);

        // The blocked author also vanishes from the blocked viewer's user-posts listing.
        var bPostsSeenByA = await aClient.GetAsync($"/api/v1/users/{bUsername}/posts");
        Assert.Equal(HttpStatusCode.NotFound, bPostsSeenByA.StatusCode);
    }

    [Fact]
    public async Task Block_HidesUserInThirdPartyFollowerLists()
    {
        var (aClient, _) = await RegisterClient();
        var (bClient, bUsername) = await RegisterClient();
        var (_, cUsername) = await RegisterClient();
        await aClient.PostAsync($"/api/v1/users/{cUsername}/follow", content: null);
        await bClient.PostAsync($"/api/v1/users/{cUsername}/follow", content: null);

        await aClient.PostAsync($"/api/v1/users/{bUsername}/block", content: null);

        // C's follower list is filtered per viewer: A no longer sees B in it.
        var followers = await aClient.GetFromJsonAsync<FollowListResponse>($"/api/v1/users/{cUsername}/followers");
        Assert.DoesNotContain(followers!.Users, u => u.Username == bUsername);

        // An anonymous viewer still sees both followers.
        var unfiltered = await factory.CreateClient()
            .GetFromJsonAsync<FollowListResponse>($"/api/v1/users/{cUsername}/followers");
        Assert.Contains(unfiltered!.Users, u => u.Username == bUsername);
    }

    [Fact]
    public async Task Unblock_RestoresContentVisibility_ForBothSides()
    {
        var (aClient, aUsername) = await RegisterClient();
        var (bClient, bUsername) = await RegisterClient();
        var (cClient, _) = await RegisterClient();
        var postId = await CreatePost(cClient, "neutral ground");
        await bClient.PostAsJsonAsync($"/api/v1/posts/{postId}/comments", new { content = "from b" });
        await aClient.PostAsync($"/api/v1/users/{bUsername}/block", content: null);

        await aClient.DeleteAsync($"/api/v1/users/{bUsername}/block");

        // One unblock removes the only block row, so BOTH sides see each other again —
        // the blocked side does not need to do anything.
        var comments = await aClient.GetFromJsonAsync<CommentFeedResponse>($"/api/v1/posts/{postId}/comments");
        Assert.Contains(comments!.Comments, c => c.Content == "from b");
        var aProfileSeenByB = await bClient.GetAsync($"/api/v1/users/{aUsername}");
        Assert.Equal(HttpStatusCode.OK, aProfileSeenByB.StatusCode);
    }

    [Fact]
    public async Task Block_WhenAlreadyBlockedByTarget_StillSucceeds()
    {
        // B blocked A first. A placing their own block row must work (and reveal nothing):
        // it protects A even if B later unblocks.
        var (aClient, aUsername) = await RegisterClient();
        var (bClient, bUsername) = await RegisterClient();
        await bClient.PostAsync($"/api/v1/users/{aUsername}/block", content: null);

        var counterBlock = await aClient.PostAsync($"/api/v1/users/{bUsername}/block", content: null);
        Assert.Equal(HttpStatusCode.NoContent, counterBlock.StatusCode);

        // B unblocks — but A's own block still stands, in both directions.
        await bClient.DeleteAsync($"/api/v1/users/{aUsername}/block");
        var bSeenByA = await aClient.GetAsync($"/api/v1/users/{bUsername}");
        Assert.Equal(HttpStatusCode.NotFound, bSeenByA.StatusCode);
        var aSeenByB = await bClient.GetAsync($"/api/v1/users/{aUsername}");
        Assert.Equal(HttpStatusCode.NotFound, aSeenByB.StatusCode);
    }
}
