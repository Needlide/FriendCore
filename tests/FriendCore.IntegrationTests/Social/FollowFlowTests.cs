using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FriendCore.Application.Common.Models;
using FriendCore.IntegrationTests.Infrastructure;

namespace FriendCore.IntegrationTests.Social;

public class FollowFlowTests(FriendCoreApiFactory factory) : IClassFixture<FriendCoreApiFactory>
{
    private async Task<HttpClient> RegisterClient(string username) =>
        (await new AuthScenarios(factory).RegisterVerifiedClientAsync(username)).Client;

    private async Task RegisterUser(string username)
    {
        await AuthScenarios.Register(factory.CreateClient(), AuthScenarios.UniqueEmail(), username);
    }

    [Fact]
    public async Task Profile_ReportsFollowState_BothWaysAndForSelf()
    {
        // The follow button used to revert to "Follow" after a refresh because the profile
        // payload carried no relationship for the client to restore from.
        var aUsername = AuthScenarios.NewUsername();
        var bUsername = AuthScenarios.NewUsername();
        var aClient = await RegisterClient(aUsername);
        await RegisterUser(bUsername);

        var before = await aClient.GetFromJsonAsync<UserProfileResponse>(
            $"/api/v1/users/{bUsername}");
        Assert.False(before!.IsFollowedByViewer);

        await aClient.PostAsync($"/api/v1/users/{bUsername}/follow", content: null);

        var after = await aClient.GetFromJsonAsync<UserProfileResponse>(
            $"/api/v1/users/{bUsername}");
        Assert.True(after!.IsFollowedByViewer);

        // The relationship is directional: B does not follow A just because A followed B.
        var bClient = await RegisterClient(AuthScenarios.NewUsername());
        var bSeesA = await bClient.GetFromJsonAsync<UserProfileResponse>(
            $"/api/v1/users/{aUsername}");
        Assert.False(bSeesA!.IsFollowedByViewer);

        // You never follow yourself.
        var selfView = await aClient.GetFromJsonAsync<UserProfileResponse>(
            $"/api/v1/users/{aUsername}");
        Assert.False(selfView!.IsFollowedByViewer);
    }

    [Fact]
    public async Task Follow_AddsToFollowersAndFollowing()
    {
        var aUsername = AuthScenarios.NewUsername();
        var bUsername = AuthScenarios.NewUsername();
        var aClient = await RegisterClient(aUsername);
        await RegisterUser(bUsername);

        var follow = await aClient.PostAsync($"/api/v1/users/{bUsername}/follow", content: null);
        Assert.Equal(HttpStatusCode.NoContent, follow.StatusCode);

        var bFollowers = await aClient.GetFromJsonAsync<FollowListResponse>($"/api/v1/users/{bUsername}/followers");
        Assert.Contains(bFollowers!.Users, u => u.Username == aUsername);

        var aFollowing = await aClient.GetFromJsonAsync<FollowListResponse>($"/api/v1/users/{aUsername}/following");
        Assert.Contains(aFollowing!.Users, u => u.Username == bUsername);
    }

    [Fact]
    public async Task Follow_IsIdempotent()
    {
        var aClient = await RegisterClient(AuthScenarios.NewUsername());
        var bUsername = AuthScenarios.NewUsername();
        await RegisterUser(bUsername);

        var first = await aClient.PostAsync($"/api/v1/users/{bUsername}/follow", content: null);
        var second = await aClient.PostAsync($"/api/v1/users/{bUsername}/follow", content: null);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);

        var followers = await aClient.GetFromJsonAsync<FollowListResponse>($"/api/v1/users/{bUsername}/followers");
        Assert.Single(followers!.Users);
    }

    [Fact]
    public async Task Unfollow_RemovesTheRelationship()
    {
        var aUsername = AuthScenarios.NewUsername();
        var bUsername = AuthScenarios.NewUsername();
        var aClient = await RegisterClient(aUsername);
        await RegisterUser(bUsername);

        await aClient.PostAsync($"/api/v1/users/{bUsername}/follow", content: null);
        var unfollow = await aClient.DeleteAsync($"/api/v1/users/{bUsername}/follow");
        Assert.Equal(HttpStatusCode.NoContent, unfollow.StatusCode);

        var followers = await aClient.GetFromJsonAsync<FollowListResponse>($"/api/v1/users/{bUsername}/followers");
        Assert.DoesNotContain(followers!.Users, u => u.Username == aUsername);
    }

    [Fact]
    public async Task Follow_Self_ReturnsBadRequest()
    {
        var username = AuthScenarios.NewUsername();
        var client = await RegisterClient(username);

        var response = await client.PostAsync($"/api/v1/users/{username}/follow", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Follow_UnknownUser_ReturnsNotFound()
    {
        var client = await RegisterClient(AuthScenarios.NewUsername());

        var response = await client.PostAsync($"/api/v1/users/{AuthScenarios.NewUsername()}/follow", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
