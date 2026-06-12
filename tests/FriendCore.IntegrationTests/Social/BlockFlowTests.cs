using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FriendCore.Application.Common.Models;
using FriendCore.IntegrationTests.Infrastructure;

namespace FriendCore.IntegrationTests.Social;

/// <summary>Spec §8: full mutual block, follow sever with server-driven confirmation, 404-not-403.</summary>
public class BlockFlowTests(FriendCoreApiFactory factory) : IClassFixture<FriendCoreApiFactory>
{
    private async Task<HttpClient> RegisterClient(string username)
    {
        factory.Clock.Reset();
        var client = factory.CreateClient();
        var email = AuthScenarios.UniqueEmail();
        await AuthScenarios.Register(client, email, username);
        var login = await AuthScenarios.Login(client, email);
        var body = await login.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        return client;
    }

    [Fact]
    public async Task Block_WithoutFollowRelationship_BlocksImmediately_AndHidesProfilesBothWays()
    {
        var aUsername = AuthScenarios.NewUsername();
        var bUsername = AuthScenarios.NewUsername();
        var aClient = await RegisterClient(aUsername);
        var bClient = await RegisterClient(bUsername);

        var block = await aClient.PostAsync($"/api/v1/users/{bUsername}/block", content: null);
        Assert.Equal(HttpStatusCode.NoContent, block.StatusCode);

        // §8.1 mutual invisibility: each side gets 404 on the other's profile.
        var bSeenByA = await aClient.GetAsync($"/api/v1/users/{bUsername}");
        Assert.Equal(HttpStatusCode.NotFound, bSeenByA.StatusCode);
        var aSeenByB = await bClient.GetAsync($"/api/v1/users/{aUsername}");
        Assert.Equal(HttpStatusCode.NotFound, aSeenByB.StatusCode);

        // Anonymous visitors are outside any block relationship.
        var anonymous = await factory.CreateClient().GetAsync($"/api/v1/users/{bUsername}");
        Assert.Equal(HttpStatusCode.OK, anonymous.StatusCode);
    }

    [Fact]
    public async Task Block_WhenFollowingTarget_RequiresConfirmation_ThenSeversFollow()
    {
        var aUsername = AuthScenarios.NewUsername();
        var bUsername = AuthScenarios.NewUsername();
        var aClient = await RegisterClient(aUsername);
        await RegisterClient(bUsername);
        await aClient.PostAsync($"/api/v1/users/{bUsername}/follow", content: null);

        var unconfirmed = await aClient.PostAsync($"/api/v1/users/{bUsername}/block", content: null);
        Assert.Equal(HttpStatusCode.Conflict, unconfirmed.StatusCode);
        Assert.Contains("User.BlockRequiresConfirmation", await unconfirmed.Content.ReadAsStringAsync());

        // The unconfirmed attempt must not have touched the follow.
        var stillFollowing =
            await aClient.GetFromJsonAsync<FollowListResponse>($"/api/v1/users/{aUsername}/following");
        Assert.Contains(stillFollowing!.Users, u => u.Username == bUsername);

        var confirmed = await aClient.PostAsJsonAsync(
            $"/api/v1/users/{bUsername}/block", new { confirmed = true });
        Assert.Equal(HttpStatusCode.NoContent, confirmed.StatusCode);

        var following = await aClient.GetFromJsonAsync<FollowListResponse>($"/api/v1/users/{aUsername}/following");
        Assert.DoesNotContain(following!.Users, u => u.Username == bUsername);
    }

    [Fact]
    public async Task Block_WhenTargetFollowsYou_RequiresConfirmation_ThenSeversTheirFollow()
    {
        var aUsername = AuthScenarios.NewUsername();
        var bUsername = AuthScenarios.NewUsername();
        var aClient = await RegisterClient(aUsername);
        var bClient = await RegisterClient(bUsername);
        await bClient.PostAsync($"/api/v1/users/{aUsername}/follow", content: null);

        var unconfirmed = await aClient.PostAsync($"/api/v1/users/{bUsername}/block", content: null);
        Assert.Equal(HttpStatusCode.Conflict, unconfirmed.StatusCode);

        var confirmed = await aClient.PostAsJsonAsync(
            $"/api/v1/users/{bUsername}/block", new { confirmed = true });
        Assert.Equal(HttpStatusCode.NoContent, confirmed.StatusCode);

        var followers = await aClient.GetFromJsonAsync<FollowListResponse>($"/api/v1/users/{aUsername}/followers");
        Assert.DoesNotContain(followers!.Users, u => u.Username == bUsername);
    }

    [Fact]
    public async Task Follow_WhileBlocked_ReturnsNotFound_ForBothSides()
    {
        var aUsername = AuthScenarios.NewUsername();
        var bUsername = AuthScenarios.NewUsername();
        var aClient = await RegisterClient(aUsername);
        var bClient = await RegisterClient(bUsername);
        await aClient.PostAsync($"/api/v1/users/{bUsername}/block", content: null);

        // The blocked side sees a 404 (never a 403 — they must not learn about the block),
        // and the blocker is equally unable to follow without unblocking first.
        var blockedFollows = await bClient.PostAsync($"/api/v1/users/{aUsername}/follow", content: null);
        Assert.Equal(HttpStatusCode.NotFound, blockedFollows.StatusCode);
        var blockerFollows = await aClient.PostAsync($"/api/v1/users/{bUsername}/follow", content: null);
        Assert.Equal(HttpStatusCode.NotFound, blockerFollows.StatusCode);
    }

    [Fact]
    public async Task Unblock_RestoresVisibility_ButNotSeveredFollows()
    {
        var aUsername = AuthScenarios.NewUsername();
        var bUsername = AuthScenarios.NewUsername();
        var aClient = await RegisterClient(aUsername);
        await RegisterClient(bUsername);
        await aClient.PostAsync($"/api/v1/users/{bUsername}/follow", content: null);
        await aClient.PostAsJsonAsync($"/api/v1/users/{bUsername}/block", new { confirmed = true });

        var unblock = await aClient.DeleteAsync($"/api/v1/users/{bUsername}/block");
        Assert.Equal(HttpStatusCode.NoContent, unblock.StatusCode);

        var profile = await aClient.GetAsync($"/api/v1/users/{bUsername}");
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);

        // Clean slate: the severed follow stays gone until explicitly re-created.
        var following = await aClient.GetFromJsonAsync<FollowListResponse>($"/api/v1/users/{aUsername}/following");
        Assert.DoesNotContain(following!.Users, u => u.Username == bUsername);

        var refollow = await aClient.PostAsync($"/api/v1/users/{bUsername}/follow", content: null);
        Assert.Equal(HttpStatusCode.NoContent, refollow.StatusCode);
    }

    [Fact]
    public async Task Block_IsIdempotent()
    {
        var bUsername = AuthScenarios.NewUsername();
        var aClient = await RegisterClient(AuthScenarios.NewUsername());
        await RegisterClient(bUsername);

        var first = await aClient.PostAsync($"/api/v1/users/{bUsername}/block", content: null);
        var second = await aClient.PostAsync($"/api/v1/users/{bUsername}/block", content: null);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
    }

    [Fact]
    public async Task Unblock_WithoutExistingBlock_IsIdempotent()
    {
        var bUsername = AuthScenarios.NewUsername();
        var aClient = await RegisterClient(AuthScenarios.NewUsername());
        await RegisterClient(bUsername);

        var response = await aClient.DeleteAsync($"/api/v1/users/{bUsername}/block");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Block_Self_ReturnsBadRequest()
    {
        var username = AuthScenarios.NewUsername();
        var client = await RegisterClient(username);

        var response = await client.PostAsync($"/api/v1/users/{username}/block", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Block_UnknownUser_ReturnsNotFound()
    {
        var client = await RegisterClient(AuthScenarios.NewUsername());

        var response = await client.PostAsync($"/api/v1/users/{AuthScenarios.NewUsername()}/block", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
