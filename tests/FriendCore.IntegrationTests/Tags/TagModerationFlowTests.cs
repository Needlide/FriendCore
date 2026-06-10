using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Tags;
using FriendCore.Domain.Users;
using FriendCore.IntegrationTests.Infrastructure;

namespace FriendCore.IntegrationTests.Tags;

public class TagModerationFlowTests(FriendCoreApiFactory factory) : IClassFixture<FriendCoreApiFactory>
{
    private readonly AuthScenarios _scenarios = new(factory);

    private async Task<HttpClient> ClientFor(string email, UserRole? role = null)
    {
        factory.Clock.Reset();
        var client = factory.CreateClient();
        var token = role is { } r
            ? await _scenarios.RegisterWithRoleAndGetToken(client, email, r)
            : await AuthScenarios.RegisterAndGetToken(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<string> CreateTag(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/v1/tags", new { name });
        var body = await response.Content.ReadFromJsonAsync<TagResponse>();
        return body!.Id;
    }

    [Fact]
    public async Task ModerationQueue_ForbiddenForRegularUser()
    {
        var client = await ClientFor(AuthScenarios.UniqueEmail());

        var response = await client.GetAsync("/api/v1/tags/moderation/queue");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ModerationQueue_ListsPendingTagsForModerator()
    {
        var authorClient = await ClientFor(AuthScenarios.UniqueEmail());
        await CreateTag(authorClient, "Queue Me");

        var moderator = await ClientFor(AuthScenarios.UniqueEmail(), UserRole.Moderator);
        var queue = await moderator.GetFromJsonAsync<TagModerationQueueResponse>("/api/v1/tags/moderation/queue");

        Assert.Contains(queue!.Tags, t => t.Slug == "queue-me");
    }

    [Fact]
    public async Task Moderator_CanApproveTag()
    {
        var authorClient = await ClientFor(AuthScenarios.UniqueEmail());
        var tagId = await CreateTag(authorClient, "Approve Me");

        var moderator = await ClientFor(AuthScenarios.UniqueEmail(), UserRole.Moderator);
        var response = await moderator.PostAsJsonAsync($"/api/v1/tags/{tagId}/moderation", new { action = "Approve" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await _scenarios.AssertTag("approve-me", tag => Assert.Equal(TagStatus.Active, tag.Status));
    }

    [Fact]
    public async Task Moderator_CanBanTag()
    {
        var authorClient = await ClientFor(AuthScenarios.UniqueEmail());
        var tagId = await CreateTag(authorClient, "Ban Me");

        var moderator = await ClientFor(AuthScenarios.UniqueEmail(), UserRole.Moderator);
        var response = await moderator.PostAsJsonAsync($"/api/v1/tags/{tagId}/moderation", new { action = "Ban" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await _scenarios.AssertTag("ban-me", tag => Assert.Equal(TagStatus.Banned, tag.Status));
    }

    [Fact]
    public async Task AutoArchive_ForbiddenForModerator_ButAllowedForAdmin()
    {
        var slug = "stale-" + Ulid.NewUlid().ToString().ToLowerInvariant()[..10];
        await _scenarios.SeedStaleActiveTagAsync("Stale Tag", slug);

        var moderator = await ClientFor(AuthScenarios.UniqueEmail(), UserRole.Moderator);
        var forbidden = await moderator.PostAsync("/api/v1/tags/moderation/auto-archive", null);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var admin = await ClientFor(AuthScenarios.UniqueEmail(), UserRole.Admin);
        var response = await admin.PostAsync("/api/v1/tags/moderation/auto-archive", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<StaleTagsArchivedResponse>();
        Assert.True(body!.ArchivedCount >= 1);
        await _scenarios.AssertTag(slug, tag => Assert.Equal(TagStatus.Archived, tag.Status));
    }

    [Fact]
    public async Task AutoArchive_DrainsAllStaleTags_AcrossMultipleBatches()
    {
        // Seed more stale tags than the test batch size (3, set in the factory) so the sweep must
        // loop. A non-looping single-batch implementation would leave the overflow tags Active.
        var slugs = await _scenarios.SeedStaleActiveTagsAsync(7, "drain");

        var admin = await ClientFor(AuthScenarios.UniqueEmail(), UserRole.Admin);
        var response = await admin.PostAsync("/api/v1/tags/moderation/auto-archive", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<StaleTagsArchivedResponse>();
        Assert.True(body!.ArchivedCount >= slugs.Count);

        foreach (var slug in slugs)
            await _scenarios.AssertTag(slug, tag => Assert.Equal(TagStatus.Archived, tag.Status));
    }
}