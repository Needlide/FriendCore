using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FriendCore.Application.Common.Models;
using FriendCore.IntegrationTests.Infrastructure;

namespace FriendCore.IntegrationTests.Posts;

/// <summary>
///     §13.7: email verification required before posting — and ONLY posting. Unverified users
///     keep commenting, liking, and following (deliberate comfort-first decision; revisit during
///     closed testing). These tests pin both sides of that boundary.
/// </summary>
public class PostingVerificationGateTests(FriendCoreApiFactory factory) : IClassFixture<FriendCoreApiFactory>
{
    private Task<(HttpClient Client, string Username)> RegisterVerifiedClient() =>
        new AuthScenarios(factory).RegisterVerifiedClientAsync();

    private async Task<(HttpClient Client, string Email)> RegisterUnverifiedClient()
    {
        factory.Clock.Reset();
        var client = factory.CreateClient();
        var email = AuthScenarios.UniqueEmail();
        await AuthScenarios.Register(client, email, AuthScenarios.NewUsername());
        var login = await AuthScenarios.Login(client, email);
        var body = await login.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        return (client, email);
    }

    [Fact]
    public async Task UnverifiedUser_CannotPost()
    {
        var (client, _) = await RegisterUnverifiedClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/posts", new { content = "first post", sensitivity = "General" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("User.EmailNotVerified", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UnverifiedUser_CanStillCommentLikeAndFollow()
    {
        var (author, authorName) = await RegisterVerifiedClient();
        var postResponse = await author.PostAsJsonAsync(
            "/api/v1/posts", new { content = "open for interaction", sensitivity = "General" });
        var post = await postResponse.Content.ReadFromJsonAsync<PostResponse>();

        var (unverified, _) = await RegisterUnverifiedClient();

        var comment = await unverified.PostAsJsonAsync(
            $"/api/v1/posts/{post!.Id}/comments", new { content = "still welcome here" });
        Assert.Equal(HttpStatusCode.Created, comment.StatusCode);

        var like = await unverified.PostAsync($"/api/v1/posts/{post.Id}/like", content: null);
        Assert.Equal(HttpStatusCode.OK, like.StatusCode);

        var follow = await unverified.PostAsync($"/api/v1/users/{authorName}/follow", content: null);
        Assert.Equal(HttpStatusCode.NoContent, follow.StatusCode);
    }

    [Fact]
    public async Task VerifyingEmail_UnlocksPosting()
    {
        var (client, email) = await RegisterUnverifiedClient();
        var blocked = await client.PostAsJsonAsync(
            "/api/v1/posts", new { content = "too early", sensitivity = "General" });
        Assert.Equal(HttpStatusCode.BadRequest, blocked.StatusCode);

        await new AuthScenarios(factory).MarkVerifiedAsync(email);

        var allowed = await client.PostAsJsonAsync(
            "/api/v1/posts", new { content = "verified now", sensitivity = "General" });
        Assert.Equal(HttpStatusCode.Created, allowed.StatusCode);
    }
}
