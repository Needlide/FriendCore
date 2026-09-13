using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Tags;
using FriendCore.IntegrationTests.Infrastructure;

namespace FriendCore.IntegrationTests.Tags;

public class TagFlowTests(FriendCoreApiFactory factory) : IClassFixture<FriendCoreApiFactory>
{
    private readonly AuthScenarios _scenarios = new(factory);

    private async Task<HttpClient> AuthenticatedClient(string email)
    {
        factory.Clock.Reset();
        var client = factory.CreateClient();
        var token = await AuthScenarios.RegisterAndGetToken(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task CreateOrGet_CreatesPendingTag_ThenReturnsSameTagOnRepeat()
    {
        var client = await AuthenticatedClient(AuthScenarios.UniqueEmail());

        var first = await client.PostAsJsonAsync("/api/v1/tags", new { name = "Indie Game Dev" });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstBody = await first.Content.ReadFromJsonAsync<TagResponse>();
        Assert.Equal("indie-game-dev", firstBody!.Slug);
        Assert.Equal(nameof(TagStatus.PendingReview), firstBody.Status);

        var second = await client.PostAsJsonAsync("/api/v1/tags", new { name = "  indie game dev  " });
        var secondBody = await second.Content.ReadFromJsonAsync<TagResponse>();
        Assert.Equal(firstBody.Id, secondBody!.Id);
    }

    [Fact]
    public async Task Autocomplete_HidesPendingTags_ButShowsApproved()
    {
        var client = await AuthenticatedClient(AuthScenarios.UniqueEmail());
        await client.PostAsJsonAsync("/api/v1/tags", new { name = "Watercolour" });

        var beforeApproval =
            await client.GetFromJsonAsync<TagAutocompleteResponse>("/api/v1/tags/autocomplete?query=water");
        Assert.DoesNotContain(beforeApproval!.Suggestions, s => s.Slug == "watercolour");

        await _scenarios.ApproveTagAsync("watercolour");

        var afterApproval =
            await client.GetFromJsonAsync<TagAutocompleteResponse>("/api/v1/tags/autocomplete?query=watercolour");
        Assert.Contains(afterApproval!.Suggestions, s => s.Slug == "watercolour");
        Assert.True(afterApproval.HasExactMatch);
    }

    [Fact]
    public async Task GetMyProfileTags_RoundTripsTiersAndVisibility()
    {
        // Distinctive tag names: tag usage counts are global per test-class database,
        // so reusing another test's tags would pollute its count assertions.
        var client = await AuthenticatedClient(AuthScenarios.UniqueEmail());

        var empty = await client.GetFromJsonAsync<ProfileTagsResponse>("/api/v1/users/me/tags");
        Assert.Empty(empty!.Tags);

        var update = await client.PutAsJsonAsync("/api/v1/users/me/tags", new
        {
            tags = new[]
            {
                new { name = "Zymurgy", tier = "Primary", isPublic = true },
                new { name = "Xylophone", tier = "Secondary", isPublic = false }
            }
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var fetched = await client.GetFromJsonAsync<ProfileTagsResponse>("/api/v1/users/me/tags");
        Assert.Equal(2, fetched!.Tags.Count);
        var primary = Assert.Single(fetched.Tags, t => t.Slug == "zymurgy");
        Assert.Equal("Primary", primary.Tier);
        Assert.True(primary.IsPublic);
        var secondary = Assert.Single(fetched.Tags, t => t.Slug == "xylophone");
        Assert.Equal("Secondary", secondary.Tier);
        Assert.False(secondary.IsPublic);
    }

    [Fact]
    public async Task UpdateProfileTags_PersistsTagsAndIncrementsCounts()
    {
        var email = AuthScenarios.UniqueEmail();
        var client = await AuthenticatedClient(email);

        var response = await client.PutAsJsonAsync("/api/v1/users/me/tags", new
        {
            tags = new[]
            {
                new { name = "Photography", tier = "Primary", isPublic = true },
                new { name = "Hiking", tier = "Secondary", isPublic = false }
            }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ProfileTagsResponse>();
        Assert.Equal(2, body!.Tags.Count);

        await _scenarios.AssertProfileTags(email, tags => Assert.Equal(2, tags.Count));
        await _scenarios.AssertTag("photography", tag => Assert.Equal(1, tag.ProfileCount));
    }

    [Fact]
    public async Task UpdateProfileTags_RemovingATag_DecrementsItsCount()
    {
        var email = AuthScenarios.UniqueEmail();
        var client = await AuthenticatedClient(email);

        await client.PutAsJsonAsync("/api/v1/users/me/tags", new
        {
            tags = new[]
            {
                new { name = "Keep Me", tier = "Primary", isPublic = true },
                new { name = "Drop Me", tier = "Primary", isPublic = true }
            }
        });

        await client.PutAsJsonAsync("/api/v1/users/me/tags", new
        {
            tags = new[]
            {
                new { name = "Keep Me", tier = "Primary", isPublic = true }
            }
        });

        await _scenarios.AssertProfileTags(email, tags =>
        {
            var tag = Assert.Single(tags);
            Assert.Equal(TagTier.Primary, tag.Tier);
        });
        await _scenarios.AssertTag("keep-me", tag => Assert.Equal(1, tag.ProfileCount));
        await _scenarios.AssertTag("drop-me", tag => Assert.Equal(0, tag.ProfileCount));
    }

    [Fact]
    public async Task UpdateProfileTags_RejectsTooManyPrimaryTags()
    {
        var client = await AuthenticatedClient(AuthScenarios.UniqueEmail());

        var tags = Enumerable.Range(0, TagLimits.MaxPrimaryProfileTags + 1)
            .Select(i => new { name = $"primary tag {i}", tier = "Primary", isPublic = true })
            .ToArray();

        var response = await client.PutAsJsonAsync("/api/v1/users/me/tags", new { tags });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Tag.TooManyPrimary", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Autocomplete_RequiresAuthentication()
    {
        factory.Clock.Reset();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/tags/autocomplete?query=photo");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}