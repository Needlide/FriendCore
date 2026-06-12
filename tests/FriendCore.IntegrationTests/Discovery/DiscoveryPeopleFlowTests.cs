using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FriendCore.Application.Common.Models;
using FriendCore.IntegrationTests.Infrastructure;

namespace FriendCore.IntegrationTests.Discovery;

/// <summary>
///     Spec §9.2 people tab + §6.6 matching: +10 per shared tag the viewer holds as primary,
///     +3 per shared secondary; excludes self, followed, muted, and blocked (both directions).
/// </summary>
public class DiscoveryPeopleFlowTests(FriendCoreApiFactory factory) : IClassFixture<FriendCoreApiFactory>
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

    private static async Task SetTags(HttpClient client, params (string Name, string Tier, bool IsPublic)[] tags)
    {
        var response = await client.PutAsJsonAsync(
            "/api/v1/users/me/tags",
            new { tags = tags.Select(t => new { name = t.Name, tier = t.Tier, isPublic = t.IsPublic }).ToArray() });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static string UniqueTag() => $"tag-{Ulid.NewUlid().ToString().ToLowerInvariant()}";

    private static async Task<DiscoveryPeopleResponse> GetPeople(HttpClient client)
    {
        return (await client.GetFromJsonAsync<DiscoveryPeopleResponse>("/api/v1/feed/discovery/people"))!;
    }

    [Fact]
    public async Task People_AreRankedByViewerTierWeights()
    {
        // Unique tags per test so parallel fixtures can never cross-match.
        var tagX = UniqueTag();
        var tagZ = UniqueTag();
        var tagUnrelated = UniqueTag();

        var (viewer, _) = await RegisterClient();
        await SetTags(viewer, (tagX, "Primary", true), (tagZ, "Secondary", true));

        // sharesPrimary holds tagX as THEIR secondary — only the viewer's tier sets the weight.
        var (sharesPrimary, sharesPrimaryName) = await RegisterClient();
        await SetTags(sharesPrimary, (tagX, "Secondary", true));
        var (sharesSecondary, sharesSecondaryName) = await RegisterClient();
        await SetTags(sharesSecondary, (tagZ, "Primary", true));
        var (sharesBoth, sharesBothName) = await RegisterClient();
        await SetTags(sharesBoth, (tagX, "Primary", true), (tagZ, "Secondary", true));
        var (noOverlap, noOverlapName) = await RegisterClient();
        await SetTags(noOverlap, (tagUnrelated, "Primary", true));

        var people = (await GetPeople(viewer)).People;

        Assert.Equal(
            [sharesBothName, sharesPrimaryName, sharesSecondaryName],
            people.Select(p => p.Username).ToArray());
        Assert.Equal([13, 10, 3], people.Select(p => p.MatchScore).ToArray());
        Assert.DoesNotContain(people, p => p.Username == noOverlapName);
    }

    [Fact]
    public async Task People_ExcludesFollowedMutedAndBlockedBothWays()
    {
        var tag = UniqueTag();
        var (viewer, viewerName) = await RegisterClient();
        await SetTags(viewer, (tag, "Primary", true));

        var candidates = new List<(HttpClient Client, string Name)>();
        for (var i = 0; i < 5; i++)
        {
            var candidate = await RegisterClient();
            await SetTags(candidate.Client, (tag, "Primary", true));
            candidates.Add(candidate);
        }

        var (followed, muted, blockedByViewer, blocksTheViewer, clean) =
            (candidates[0], candidates[1], candidates[2], candidates[3], candidates[4]);
        await viewer.PostAsync($"/api/v1/users/{followed.Name}/follow", content: null);
        await viewer.PostAsync($"/api/v1/users/{muted.Name}/mute", content: null);
        await viewer.PostAsync($"/api/v1/users/{blockedByViewer.Name}/block", content: null);
        await blocksTheViewer.Client.PostAsync($"/api/v1/users/{viewerName}/block", content: null);

        var people = (await GetPeople(viewer)).People;

        var single = Assert.Single(people);
        Assert.Equal(clean.Name, single.Username);
    }

    [Fact]
    public async Task People_PrivateTagsStillMatch_ButAreNeverRevealed()
    {
        var tag = UniqueTag();
        var (viewer, _) = await RegisterClient();
        await SetTags(viewer, (tag, "Primary", false));
        var (candidate, candidateName) = await RegisterClient();
        await SetTags(candidate, (tag, "Secondary", false));

        var people = (await GetPeople(viewer)).People;

        // Both sides keep the tag private; matching works (§6.4), and the response carries no
        // tag information at all — only the opaque score.
        var single = Assert.Single(people);
        Assert.Equal(candidateName, single.Username);
        Assert.Equal(10, single.MatchScore);
    }

    [Fact]
    public async Task People_WithNoProfileTags_ReturnsEmpty()
    {
        // Cold-start fallback (§9.3) is deliberately deferred; today a tagless viewer gets [].
        var (viewer, _) = await RegisterClient();

        var people = (await GetPeople(viewer)).People;

        Assert.Empty(people);
    }

    [Fact]
    public async Task People_RequiresAuth()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/feed/discovery/people");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
