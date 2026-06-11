using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FriendCore.Application.Common.Models;
using FriendCore.IntegrationTests.Infrastructure;
using FriendCore.IntegrationTests.Media;

namespace FriendCore.IntegrationTests.Posts;

/// <summary>
///     Spec §5.3/§7 end to end: upload → process → attach to post → media (with public variant
///     URLs) flows through post detail and the home feed.
/// </summary>
public class PostMediaFlowTests(FriendCoreApiFactory factory) : IClassFixture<FriendCoreApiFactory>
{
    private async Task<(HttpClient Client, string Username)> AuthedClient()
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

    /// <summary>Uploads a real image and waits until it is Ready; returns its id.</summary>
    private static async Task<string> UploadReadyMedia(HttpClient client, int width = 200, int height = 100)
    {
        var file = new ByteArrayContent(TestImages.RealPng(width, height));
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        var form = new MultipartFormDataContent { { file, "file", "image.png" } };

        var response = await client.PostAsync("/api/v1/media", form);
        var body = await response.Content.ReadFromJsonAsync<MediaStatusResponse>();
        Assert.Equal("Ready", await MediaUploadFlowTests.PollStatusAsync(client, body!.Id));
        return body.Id;
    }

    private static async Task<HttpResponseMessage> CreatePost(
        HttpClient client,
        string? content,
        params string[] mediaIds)
    {
        return await client.PostAsJsonAsync(
            "/api/v1/posts",
            new { content, sensitivity = "General", mediaIds });
    }

    [Fact]
    public async Task CreatePost_WithMedia_ReturnsOrderedVariantUrls_OnDetailAndFeed()
    {
        var (author, authorName) = await AuthedClient();
        var firstId = await UploadReadyMedia(author);
        var secondId = await UploadReadyMedia(author);

        var created = await CreatePost(author, "look at these", firstId, secondId);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var post = await created.Content.ReadFromJsonAsync<PostResponse>();

        // Media in request order, with public URLs composed from PublicBaseUrl + storage key.
        Assert.Equal(new[] { firstId, secondId }, post!.Media.Select(m => m.Id));
        var medium = post.Media[0].Variants.Single(v => v.Type == "Medium");
        Assert.EndsWith($"uploads/variants/{firstId}/medium.webp", medium.Url);
        Assert.StartsWith("http", medium.Url);
        // Only the four public WebP variants — the original is never exposed by URL.
        Assert.DoesNotContain(post.Media[0].Variants, v => v.Type == "Original");
        Assert.Equal(4, post.Media[0].Variants.Count);

        // Post detail carries the same media.
        var detail = await author.GetFromJsonAsync<PostResponse>($"/api/v1/posts/{post.Id}");
        Assert.Equal(2, detail!.Media.Count);

        // And the follower's home feed does too.
        var (follower, _) = await AuthedClient();
        await follower.PostAsync($"/api/v1/users/{authorName}/follow", null);
        var feed = await follower.GetFromJsonAsync<PostFeedResponse>("/api/v1/feed");
        var feedPost = Assert.Single(feed!.Posts);
        Assert.Equal(2, feedPost.Media.Count);
        Assert.Equal(new[] { firstId, secondId }, feedPost.Media.Select(m => m.Id));
    }

    [Fact]
    public async Task CreatePost_ImageOnly_WithoutContent_Succeeds()
    {
        var (author, _) = await AuthedClient();
        var mediaId = await UploadReadyMedia(author);

        var created = await CreatePost(author, null, mediaId);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var post = await created.Content.ReadFromJsonAsync<PostResponse>();
        Assert.Null(post!.Content);
        Assert.Single(post.Media);
    }

    [Fact]
    public async Task CreatePost_WithProcessingMedia_ReturnsBadRequest()
    {
        var (author, _) = await AuthedClient();

        // Corrupt-after-magic-bytes upload: stays out of Ready (fails processing), so attach must
        // be rejected. Grab the id straight from the upload response without waiting.
        byte[] corrupt = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0xDE, 0xAD, 0xBE, 0xEF];
        var file = new ByteArrayContent(corrupt);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        var upload = await author.PostAsync(
            "/api/v1/media", new MultipartFormDataContent { { file, "file", "x.png" } });
        var media = await upload.Content.ReadFromJsonAsync<MediaStatusResponse>();

        var created = await CreatePost(author, "too eager", media!.Id);

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);
        Assert.Contains("Media.NotReady", await created.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task CreatePost_WithSomeoneElsesMedia_ReturnsForbidden()
    {
        var (owner, _) = await AuthedClient();
        var mediaId = await UploadReadyMedia(owner);

        var (thief, _) = await AuthedClient();
        var created = await CreatePost(thief, "not mine", mediaId);

        Assert.Equal(HttpStatusCode.Forbidden, created.StatusCode);
    }

    [Fact]
    public async Task CreatePost_ReusingAttachedMedia_ReturnsConflict()
    {
        var (author, _) = await AuthedClient();
        var mediaId = await UploadReadyMedia(author);

        var first = await CreatePost(author, "first", mediaId);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await CreatePost(author, "second", mediaId);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Contains("Media.AlreadyAttached", await second.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task CreatePost_WithNeitherContentNorMedia_ReturnsBadRequest()
    {
        var (author, _) = await AuthedClient();

        var created = await CreatePost(author, null);

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);
        Assert.Contains("Post.ContentOrMediaRequired", await created.Content.ReadAsStringAsync());
    }
}
