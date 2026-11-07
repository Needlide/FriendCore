using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FriendCore.Application.Common.Models;
using FriendCore.IntegrationTests.Infrastructure;

namespace FriendCore.IntegrationTests.Posts;

public class PostInteractionFlowTests(FriendCoreApiFactory factory) : IClassFixture<FriendCoreApiFactory>
{
    private async Task<HttpClient> AuthedClient() =>
        (await new AuthScenarios(factory).RegisterVerifiedClientAsync()).Client;

    private Task<(HttpClient Client, string Username)> RegisterClient() =>
        new AuthScenarios(factory).RegisterVerifiedClientAsync();

    private static async Task<string> CreatePost(HttpClient client, string content = "a post")
    {
        var response = await client.PostAsJsonAsync("/api/v1/posts", new { content, sensitivity = "General" });
        var body = await response.Content.ReadFromJsonAsync<PostResponse>();
        return body!.Id;
    }

    [Fact]
    public async Task Like_ThenUnlike_UpdatesCountAndFlag()
    {
        var client = await AuthedClient();
        var postId = await CreatePost(client);

        var like = await client.PostAsync($"/api/v1/posts/{postId}/like", content: null);
        Assert.Equal(HttpStatusCode.OK, like.StatusCode);
        var likeBody = await like.Content.ReadFromJsonAsync<PostLikeResponse>();
        Assert.True(likeBody!.Liked);
        Assert.Equal(1, likeBody.LikeCount);

        var liked = await client.GetFromJsonAsync<PostResponse>($"/api/v1/posts/{postId}");
        Assert.Equal(1, liked!.LikeCount);
        Assert.True(liked.IsLikedByCurrentUser);

        var unlike = await client.DeleteAsync($"/api/v1/posts/{postId}/like");
        Assert.Equal(HttpStatusCode.OK, unlike.StatusCode);
        var unlikeBody = await unlike.Content.ReadFromJsonAsync<PostLikeResponse>();
        Assert.False(unlikeBody!.Liked);
        Assert.Equal(0, unlikeBody.LikeCount);

        var unliked = await client.GetFromJsonAsync<PostResponse>($"/api/v1/posts/{postId}");
        Assert.Equal(0, unliked!.LikeCount);
        Assert.False(unliked.IsLikedByCurrentUser);
    }

    [Fact]
    public async Task Like_IsIdempotent()
    {
        var client = await AuthedClient();
        var postId = await CreatePost(client);

        await client.PostAsync($"/api/v1/posts/{postId}/like", content: null);
        var second = await client.PostAsync($"/api/v1/posts/{postId}/like", content: null);

        // The repeated like is a no-op but still reports the stable authoritative state.
        var secondBody = await second.Content.ReadFromJsonAsync<PostLikeResponse>();
        Assert.True(secondBody!.Liked);
        Assert.Equal(1, secondBody.LikeCount);

        var post = await client.GetFromJsonAsync<PostResponse>($"/api/v1/posts/{postId}");
        Assert.Equal(1, post!.LikeCount);
    }

    [Fact]
    public async Task Unlike_IsIdempotent()
    {
        var client = await AuthedClient();
        var postId = await CreatePost(client);

        // Unliking a post you never liked is a no-op but still reports stable state.
        var first = await client.DeleteAsync($"/api/v1/posts/{postId}/like");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstBody = await first.Content.ReadFromJsonAsync<PostLikeResponse>();
        Assert.False(firstBody!.Liked);
        Assert.Equal(0, firstBody.LikeCount);

        await client.PostAsync($"/api/v1/posts/{postId}/like", content: null);
        await client.DeleteAsync($"/api/v1/posts/{postId}/like");
        var second = await client.DeleteAsync($"/api/v1/posts/{postId}/like");

        var secondBody = await second.Content.ReadFromJsonAsync<PostLikeResponse>();
        Assert.False(secondBody!.Liked);
        Assert.Equal(0, secondBody.LikeCount);

        var post = await client.GetFromJsonAsync<PostResponse>($"/api/v1/posts/{postId}");
        Assert.Equal(0, post!.LikeCount);
    }

    [Fact]
    public async Task LikesList_ShowsLikers_NewestFirst_AndClearsAfterUnlike()
    {
        var (author, _) = await RegisterClient();
        var (alice, aliceName) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var postId = await CreatePost(author);
        await alice.PostAsync($"/api/v1/posts/{postId}/like", content: null);
        await bob.PostAsync($"/api/v1/posts/{postId}/like", content: null);

        var likes = await author.GetFromJsonAsync<LikeListResponse>($"/api/v1/posts/{postId}/likes");
        Assert.Equal(2, likes!.Users.Count);
        Assert.Contains(likes.Users, user => user.Username == aliceName);
        Assert.Contains(likes.Users, user => user.Username == bobName);

        // The list is anonymous-readable, like the post it belongs to.
        var anonymous = factory.CreateClient();
        var publicLikes = await anonymous.GetFromJsonAsync<LikeListResponse>(
            $"/api/v1/posts/{postId}/likes");
        Assert.Equal(2, publicLikes!.Users.Count);

        await alice.DeleteAsync($"/api/v1/posts/{postId}/like");
        var after = await author.GetFromJsonAsync<LikeListResponse>($"/api/v1/posts/{postId}/likes");
        var remaining = Assert.Single(after!.Users);
        Assert.Equal(bobName, remaining.Username);
    }

    [Fact]
    public async Task LikesList_OmitsLikersHiddenByABlock()
    {
        var (author, authorName) = await RegisterClient();
        var (liker, likerName) = await RegisterClient();
        var postId = await CreatePost(author);

        await liker.PostAsync($"/api/v1/posts/{postId}/like", content: null);
        Assert.Single((await author.GetFromJsonAsync<LikeListResponse>(
            $"/api/v1/posts/{postId}/likes"))!.Users);

        // A block hides the liker from the author's user lookups. The denormalised count may
        // still include them (accepted MVP trade-off), but the list must not expose them.
        await liker.PostAsync($"/api/v1/users/{authorName}/block", content: null);

        var likes = await author.GetFromJsonAsync<LikeListResponse>($"/api/v1/posts/{postId}/likes");
        Assert.Empty(likes!.Users);
        var post = await author.GetFromJsonAsync<PostResponse>($"/api/v1/posts/{postId}");
        Assert.Equal(1, post!.LikeCount);
        Assert.DoesNotContain(likerName, likes.Users.Select(user => user.Username));
    }

    [Fact]
    public async Task LikesList_ReturnsNotFound_ForMissingOrBlockedPost()
    {
        var (author, authorName) = await RegisterClient();
        var (viewer, _) = await RegisterClient();
        var postId = await CreatePost(author);

        var missing = await viewer.GetAsync($"/api/v1/posts/{Ulid.NewUlid()}/likes");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        // Blocking the author hides the post everywhere, including its likers list.
        await viewer.PostAsync($"/api/v1/users/{authorName}/block", content: null);
        var hidden = await viewer.GetAsync($"/api/v1/posts/{postId}/likes");
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
    }

    [Fact]
    public async Task AddComment_ThenList_ThenDelete_UpdatesCount()
    {
        var client = await AuthedClient();
        var postId = await CreatePost(client);

        var add = await client.PostAsJsonAsync($"/api/v1/posts/{postId}/comments", new { content = "great post" });
        Assert.Equal(HttpStatusCode.Created, add.StatusCode);
        var comment = await add.Content.ReadFromJsonAsync<CommentResponse>();

        var afterAdd = await client.GetFromJsonAsync<PostResponse>($"/api/v1/posts/{postId}");
        Assert.Equal(1, afterAdd!.CommentCount);

        var list = await client.GetFromJsonAsync<CommentFeedResponse>($"/api/v1/posts/{postId}/comments");
        Assert.Contains(list!.Comments, c => c.Content == "great post");

        var delete = await client.DeleteAsync($"/api/v1/comments/{comment!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var afterDelete = await client.GetFromJsonAsync<PostResponse>($"/api/v1/posts/{postId}");
        Assert.Equal(0, afterDelete!.CommentCount);
    }

    [Fact]
    public async Task DeleteComment_ForNonAuthor_ReturnsForbidden()
    {
        var author = await AuthedClient();
        var postId = await CreatePost(author);
        var add = await author.PostAsJsonAsync($"/api/v1/posts/{postId}/comments", new { content = "mine" });
        var comment = await add.Content.ReadFromJsonAsync<CommentResponse>();

        var other = await AuthedClient();
        var delete = await other.DeleteAsync($"/api/v1/comments/{comment!.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    [Fact]
    public async Task AddComment_Empty_ReturnsBadRequest()
    {
        var client = await AuthedClient();
        var postId = await CreatePost(client);

        var response = await client.PostAsJsonAsync($"/api/v1/posts/{postId}/comments", new { content = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
