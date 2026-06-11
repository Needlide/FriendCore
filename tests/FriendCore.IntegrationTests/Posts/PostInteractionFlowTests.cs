using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FriendCore.Application.Common.Models;
using FriendCore.IntegrationTests.Infrastructure;

namespace FriendCore.IntegrationTests.Posts;

public class PostInteractionFlowTests(FriendCoreApiFactory factory) : IClassFixture<FriendCoreApiFactory>
{
    private async Task<HttpClient> AuthedClient()
    {
        factory.Clock.Reset();
        var client = factory.CreateClient();
        var email = AuthScenarios.UniqueEmail();
        await AuthScenarios.Register(client, email, AuthScenarios.NewUsername());
        var login = await AuthScenarios.Login(client, email);
        var body = await login.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        return client;
    }

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
