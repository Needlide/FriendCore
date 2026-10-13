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

    /// <summary>
    ///     A keyword is typed by a person, so it may contain anything — including the characters
    ///     that mean something to a pattern matcher.
    /// </summary>
    /// <remarks>
    ///     This was a live 500. The filter was built with a provider helper that emits a POSIX
    ///     regex, so "c++" is an invalid pattern ("nothing to repeat") and the database threw,
    ///     while "_" silently matched ANY single character and hid posts the user never asked to
    ///     stop seeing. Regression test for ProviderQuirkGuardTests.
    /// </remarks>
    [Theory]
    [InlineData("c++")]          // invalid regex: nothing to repeat
    [InlineData("50%")]          // % must be literal, not a wildcard
    [InlineData("snake_case")]   // _ must be literal, not "any character"
    [InlineData("a(b|c)")]       // alternation and groups must be literal
    [InlineData("[draft]")]      // character class must be literal
    [InlineData("what?")]        // ? must be literal, not a quantifier
    public async Task A_Keyword_Containing_Pattern_Characters_Is_Matched_Literally(string keyword)
    {
        var (aClient, aUsername) = await RegisterClient();
        var (bClient, bUsername) = await RegisterClient();
        await aClient.PostAsync($"/api/v1/users/{bUsername}/follow", content: null);

        // One post the keyword should hide, and one that merely LOOKS like a match under a
        // pattern matcher — this is the case that catches "_" and "%" over-matching.
        var shouldBeHidden = await CreatePost(bClient, $"a post about {keyword} here");
        var shouldSurvive = await CreatePost(bClient, "an entirely unrelated remark");

        var patch = await aClient.PatchAsJsonAsync(
            "/api/v1/users/me/keyword-filters", new { keywords = new[] { keyword } });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);

        var feed = await aClient.GetFromJsonAsync<PostFeedResponse>("/api/v1/feed/home");

        Assert.DoesNotContain(feed!.Posts, p => p.Id == shouldBeHidden);
        Assert.Contains(feed.Posts, p => p.Id == shouldSurvive);
    }

    /// <summary>
    ///     A keyword that looks like a pattern must not hide posts it does not literally appear in.
    ///     Separate from the tests above because this is the over-matching half of the bug: the
    ///     keyword succeeds, it just hides the wrong things — which is worse than failing loudly,
    ///     because the feed quietly loses posts the user never asked to lose.
    /// </summary>
    [Fact]
    public async Task Percent_In_A_Keyword_Does_Not_Match_Any_Suffix()
    {
        var (aClient, _) = await RegisterClient();
        var (bClient, bUsername) = await RegisterClient();
        await aClient.PostAsync($"/api/v1/users/{bUsername}/follow", content: null);

        var literal = await CreatePost(bClient, "it costs 50% of nothing");
        // "50" followed by ANY character: under the old ESCAPE '' pattern `%50%%` this matched,
        // so the post was hidden even though the keyword never appears in it.
        var lookalike = await CreatePost(bClient, "50 cats were seen");

        await aClient.PatchAsJsonAsync(
            "/api/v1/users/me/keyword-filters", new { keywords = new[] { "50%" } });

        var feed = await aClient.GetFromJsonAsync<PostFeedResponse>("/api/v1/feed/home");

        Assert.DoesNotContain(feed!.Posts, p => p.Id == literal);
        Assert.Contains(feed.Posts, p => p.Id == lookalike);
    }

    [Fact]
    public async Task Underscore_In_A_Keyword_Does_Not_Match_Any_Character()
    {
        var (aClient, aUsername) = await RegisterClient();
        var (bClient, bUsername) = await RegisterClient();
        await aClient.PostAsync($"/api/v1/users/{bUsername}/follow", content: null);

        var literal = await CreatePost(bClient, "snake_case naming is fine");
        var lookalike = await CreatePost(bClient, "snakeXcase naming is different");

        await aClient.PatchAsJsonAsync(
            "/api/v1/users/me/keyword-filters", new { keywords = new[] { "snake_case" } });

        var feed = await aClient.GetFromJsonAsync<PostFeedResponse>("/api/v1/feed/home");

        Assert.DoesNotContain(feed!.Posts, p => p.Id == literal);
        // Under the old regex, "snake_case" matched "snakeXcase" and this post was wrongly hidden.
        Assert.Contains(feed.Posts, p => p.Id == lookalike);
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
