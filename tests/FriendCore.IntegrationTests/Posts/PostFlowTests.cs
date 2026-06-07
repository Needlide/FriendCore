using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FriendCore.Application.Common.Models;
using FriendCore.IntegrationTests.Infrastructure;

namespace FriendCore.IntegrationTests.Posts;

public class PostFlowTests(FriendCoreApiFactory factory) : IClassFixture<FriendCoreApiFactory>
{
    private async Task<(HttpClient Client, string Username)> AuthorClient()
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
    public async Task Create_ThenGet_ReturnsPost()
    {
        var (client, username) = await AuthorClient();

        var create = await client.PostAsJsonAsync("/api/v1/posts", new { content = "Hello world", sensitivity = "Sensitive" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<PostResponse>();
        Assert.Equal("Hello world", created!.Content);
        Assert.Equal(username, created.AuthorUsername);
        Assert.Equal("Sensitive", created.Sensitivity);
        Assert.Equal(0, created.LikeCount);

        var get = await client.GetFromJsonAsync<PostResponse>($"/api/v1/posts/{created.Id}");
        Assert.Equal("Hello world", get!.Content);
    }

    [Fact]
    public async Task Create_WithoutContent_ReturnsBadRequest()
    {
        var (client, _) = await AuthorClient();

        var response = await client.PostAsJsonAsync("/api/v1/posts", new { sensitivity = "General" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_Unauthenticated_ReturnsUnauthorized()
    {
        factory.Clock.Reset();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/posts", new { content = "hi", sensitivity = "General" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Delete_OwnPost_ThenGet_ReturnsNotFound()
    {
        var (client, _) = await AuthorClient();
        var postId = await CreatePost(client, "to be deleted");

        var delete = await client.DeleteAsync($"/api/v1/posts/{postId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var get = await client.GetAsync($"/api/v1/posts/{postId}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task Delete_OthersPost_ReturnsForbidden()
    {
        var (authorClient, _) = await AuthorClient();
        var postId = await CreatePost(authorClient, "not yours");

        var (otherClient, _) = await AuthorClient();
        var delete = await otherClient.DeleteAsync($"/api/v1/posts/{postId}");

        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    [Fact]
    public async Task GetUserPosts_ReturnsAuthorsPosts_NewestFirst()
    {
        var (client, username) = await AuthorClient();
        await CreatePost(client, "first post");
        await CreatePost(client, "second post");

        var feed = await client.GetFromJsonAsync<PostFeedResponse>($"/api/v1/users/{username}/posts");

        Assert.Equal(2, feed!.Posts.Count);
        Assert.Equal("second post", feed.Posts[0].Content);
        Assert.Equal("first post", feed.Posts[1].Content);
        Assert.Null(feed.NextCursor);
    }
}
