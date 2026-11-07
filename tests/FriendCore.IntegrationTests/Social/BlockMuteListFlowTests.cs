using System.Net;
using System.Net.Http.Json;
using FriendCore.Application.Common.Models;
using FriendCore.IntegrationTests.Infrastructure;

namespace FriendCore.IntegrationTests.Social;

/// <summary>
///     Management reads for the settings screen. The list must carry display data, not just
///     ids: a blocked profile is a 404 everywhere else, so this response is the only place the
///     UI can learn the name of the person it is offering to unblock.
/// </summary>
public class BlockMuteListFlowTests(FriendCoreApiFactory factory) : IClassFixture<FriendCoreApiFactory>
{
    private Task<(HttpClient Client, string Username)> RegisterClient() =>
        new AuthScenarios(factory).RegisterVerifiedClientAsync();

    [Fact]
    public async Task Lists_RequireAuthentication()
    {
        var anonymous = factory.CreateClient();

        var blocks = await anonymous.GetAsync("/api/v1/users/me/blocks");
        var mutes = await anonymous.GetAsync("/api/v1/users/me/mutes");

        Assert.Equal(HttpStatusCode.Unauthorized, blocks.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, mutes.StatusCode);
    }

    [Fact]
    public async Task BlockList_ShowsBlockedUser_EvenThoughTheirProfileIsNotFound()
    {
        var (aClient, _) = await RegisterClient();
        var (_, bUsername) = await RegisterClient();

        await aClient.PostAsync($"/api/v1/users/{bUsername}/block", content: null);

        // The profile is deliberately hidden…
        var profile = await aClient.GetAsync($"/api/v1/users/{bUsername}");
        Assert.Equal(HttpStatusCode.NotFound, profile.StatusCode);

        // …but the owner can still discover who they blocked and undo it.
        var list = await aClient.GetFromJsonAsync<BlockListResponse>("/api/v1/users/me/blocks");
        var blocked = Assert.Single(list!.Users);
        Assert.Equal(bUsername, blocked.Username);
        Assert.False(string.IsNullOrWhiteSpace(blocked.DisplayName));

        await aClient.DeleteAsync($"/api/v1/users/{bUsername}/block");
        var after = await aClient.GetFromJsonAsync<BlockListResponse>("/api/v1/users/me/blocks");
        Assert.Empty(after!.Users);
    }

    [Fact]
    public async Task MuteList_IsSeparateFromBlocks_AndClearsAfterUnmute()
    {
        var (aClient, _) = await RegisterClient();
        var (_, bUsername) = await RegisterClient();

        await aClient.PostAsync($"/api/v1/users/{bUsername}/mute", content: null);

        var mutes = await aClient.GetFromJsonAsync<MuteListResponse>("/api/v1/users/me/mutes");
        var muted = Assert.Single(mutes!.Users);
        Assert.Equal(bUsername, muted.Username);

        var blocks = await aClient.GetFromJsonAsync<BlockListResponse>("/api/v1/users/me/blocks");
        Assert.Empty(blocks!.Users);

        await aClient.DeleteAsync($"/api/v1/users/{bUsername}/mute");
        var after = await aClient.GetFromJsonAsync<MuteListResponse>("/api/v1/users/me/mutes");
        Assert.Empty(after!.Users);
    }

    [Fact]
    public async Task BlockList_DoesNotRevealABlockCreatedBySomeoneElse()
    {
        var (aClient, aUsername) = await RegisterClient();
        var (bClient, _) = await RegisterClient();

        // B blocks A. A must not learn this from the management list, matching the
        // 404-not-403 rule that keeps the blocked person unaware (§8.2).
        await bClient.PostAsync($"/api/v1/users/{aUsername}/block", content: null);

        var aBlocks = await aClient.GetFromJsonAsync<BlockListResponse>("/api/v1/users/me/blocks");
        Assert.Empty(aBlocks!.Users);

        // B, the blocker, sees A and can undo its own row.
        var bBlocks = await bClient.GetFromJsonAsync<BlockListResponse>("/api/v1/users/me/blocks");
        var blocked = Assert.Single(bBlocks!.Users);
        Assert.Equal(aUsername, blocked.Username);
    }

    [Fact]
    public async Task Lists_StartEmpty_AndOnlyContainTheCurrentUsersEntries()
    {
        var (aClient, _) = await RegisterClient();
        var (bClient, _) = await RegisterClient();
        var (_, cUsername) = await RegisterClient();

        var aBlocks = await aClient.GetFromJsonAsync<BlockListResponse>("/api/v1/users/me/blocks");
        var aMutes = await aClient.GetFromJsonAsync<MuteListResponse>("/api/v1/users/me/mutes");
        Assert.Empty(aBlocks!.Users);
        Assert.Empty(aMutes!.Users);

        await bClient.PostAsync($"/api/v1/users/{cUsername}/mute", content: null);

        // B's mute must not leak into A's view.
        var aMutesAfter = await aClient.GetFromJsonAsync<MuteListResponse>("/api/v1/users/me/mutes");
        Assert.Empty(aMutesAfter!.Users);
        var bMutesAfter = await bClient.GetFromJsonAsync<MuteListResponse>("/api/v1/users/me/mutes");
        Assert.Single(bMutesAfter!.Users);
    }
}
