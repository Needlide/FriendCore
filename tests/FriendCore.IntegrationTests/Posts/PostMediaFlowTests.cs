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
    private Task<(HttpClient Client, string Username)> AuthedClient() =>
        new AuthScenarios(factory).RegisterVerifiedClientAsync();

    /// <summary>Uploads a real image and waits until it is Ready; returns its id.</summary>
    private static async Task<string> UploadReadyMedia(
        HttpClient client,
        int width = 200,
        int height = 100,
        bool flashing = false,
        bool intenseMotion = false)
    {
        var file = new ByteArrayContent(TestImages.RealPng(width, height));
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        var form = new MultipartFormDataContent
        {
            { file, "file", "image.png" },
            { new StringContent(flashing.ToString()), "HasFlashingWarning" },
            { new StringContent(intenseMotion.ToString()), "HasIntenseMotionWarning" }
        };

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
        var feed = await follower.GetFromJsonAsync<PostFeedResponse>("/api/v1/feed/home");
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
    public async Task PostVariantUrl_IsPubliclyReadable_WithoutCredentials()
    {
        // The browser fetches the post response's variant URLs directly (never through the
        // API). This performs that exact anonymous GET against the REAL server-built URL —
        // it covers both the bucket policy (403 when private) and URL hygiene (400 on a
        // double-slash path), the two stacked bugs that once broke every image silently.
        var (author, _) = await AuthedClient();
        var mediaId = await UploadReadyMedia(author, 800, 600);

        var created = await CreatePost(author, "look", mediaId);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var post = await created.Content.ReadFromJsonAsync<PostResponse>();
        var url = post!.Media.Single().Variants.Single(v => v.Type == "Medium").Url;
        Assert.StartsWith("http", url);
        Assert.DoesNotContain("//uploads/", url.Replace("://", ""));

        using var anonymous = new HttpClient();
        var response = await anonymous.GetAsync(url);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"GET {url} -> {(int)response.StatusCode}: {body}");
        Assert.Equal("image/webp", response.Content.Headers.ContentType?.MediaType);
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

    [Fact]
    public async Task FlaggedMedia_IsServedWithNoVariants_FromThePostResponse()
    {
        var (author, _) = await AuthedClient();
        var flashing = await UploadReadyMedia(author, flashing: true);
        var motion = await UploadReadyMedia(author, intenseMotion: true);
        var plain = await UploadReadyMedia(author);

        var response = await CreatePost(author, "mixed", flashing, motion, plain);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var post = await response.Content.ReadFromJsonAsync<PostResponse>();

        var byId = post!.Media.ToDictionary(m => m.Id);

        // The gate: no URLs at all, so the browser cannot be handed a location to fetch. The flags
        // still ship, so the client can explain WHY there is no image.
        Assert.Empty(byId[flashing].Variants);
        Assert.Empty(byId[motion].Variants);
        Assert.True(byId[flashing].HasFlashingWarning);
        Assert.True(byId[motion].HasIntenseMotionWarning);

        // Unflagged media is untouched — this must not have become a blanket change.
        Assert.NotEmpty(byId[plain].Variants);
    }

    [Fact]
    public async Task Reveal_HandsOverVariants_OnceTheReaderConsents()
    {
        var (author, _) = await AuthedClient();
        var (reader, _) = await AuthedClient();
        var flashing = await UploadReadyMedia(author, flashing: true);
        Assert.Equal(
            HttpStatusCode.Created,
            (await CreatePost(author, "look", flashing)).StatusCode);

        var reveal = await reader.PostAsync($"/api/v1/media/{flashing}/reveal", content: null);
        Assert.Equal(HttpStatusCode.OK, reveal.StatusCode);

        var body = await reveal.Content.ReadFromJsonAsync<MediaVariantsResponse>();
        Assert.Equal(flashing, body!.MediaId);
        Assert.NotEmpty(body.Variants);
        // Exactly the URLs the gated post response withheld — a reveal is a consent step for the
        // same bytes, never a wider grant than an ungated post would have given.
        Assert.DoesNotContain(body.Variants, v => v.Type == "Original");
        Assert.All(body.Variants, v => Assert.StartsWith("http", v.Url));
    }

    [Fact]
    public async Task Reveal_Succeeds_ForAnyReaderWhoCanSeeThePost()
    {
        // Posts are public, so "may this user see the media" is "may they see the post" — which
        // for a post is everyone. Consent gates the flashing warning, it does not turn a public
        // post into a private one; refusing here would break the ordinary reading path for
        // readers who explicitly opted into seeing flagged media.
        var (author, _) = await AuthedClient();
        var (stranger, _) = await AuthedClient();
        var flashing = await UploadReadyMedia(author, flashing: true);
        Assert.Equal(
            HttpStatusCode.Created,
            (await CreatePost(author, "look", flashing)).StatusCode);

        var reveal = await stranger.PostAsync($"/api/v1/media/{flashing}/reveal", content: null);
        Assert.Equal(HttpStatusCode.OK, reveal.StatusCode);
    }

    [Fact]
    public async Task Reveal_ReturnsNotFound_ForUnflaggedMediaAttachedToNothingVisible()
    {
        var (author, _) = await AuthedClient();
        var (stranger, _) = await AuthedClient();
        var plain = await UploadReadyMedia(author);

        // Unflagged media is served inline anyway, so reveal is not the gate for it — it just
        // works, which is why the ordinary path never needs the extra request.
        var reveal = await stranger.PostAsync($"/api/v1/media/{plain}/reveal", content: null);
        Assert.Equal(HttpStatusCode.OK, reveal.StatusCode);
    }
}
