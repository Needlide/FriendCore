using System.Net;
using System.Net.Http.Json;
using FriendCore.IntegrationTests.Infrastructure;

namespace FriendCore.IntegrationTests.Auth;

/// <summary>
///     §3/§17.2 MVP: DELETE /users/me soft-deletes the account, revokes refresh tokens, and the
///     account disappears from logins and profile lookups.
/// </summary>
public class AccountDeletionFlowTests(FriendCoreApiFactory factory) : IClassFixture<FriendCoreApiFactory>
{
    private readonly AuthScenarios _scenarios = new(factory);

    [Fact]
    public async Task DeleteAccount_RemovesTheAccountFromLoginAndProfiles()
    {
        var (client, username) = await _scenarios.RegisterVerifiedClientAsync();

        var delete = await client.DeleteAsync("/api/v1/users/me");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        // Soft-deleted users are invisible to the profile endpoint.
        var profile = await factory.CreateClient().GetAsync($"/api/v1/users/{username}");
        Assert.Equal(HttpStatusCode.NotFound, profile.StatusCode);

        // And cannot sign in again with the old credentials.
        var login = await AuthScenarios.LoginByUsername(factory.CreateClient(), username);
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task DeleteAccount_Unauthenticated_ReturnsUnauthorized()
    {
        var response = await factory.CreateClient().DeleteAsync("/api/v1/users/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteAccount_ClearsTheRefreshCookie()
    {
        var client = factory.CreateClient();
        var email = AuthScenarios.UniqueEmail();
        var username = AuthScenarios.NewUsername();
        await AuthScenarios.Register(client, email, username);
        await _scenarios.MarkVerifiedAsync(email);

        var login = await _scenarios.LoginAsync(client, email);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var auth = await login.Content.ReadFromJsonAsync<FriendCore.Application.Common.Models.AuthResponse>();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var delete = await client.DeleteAsync("/api/v1/users/me");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        // A rotated refresh must not resurrect the session.
        var refresh = await client.PostAsync("/api/v1/auth/refresh", content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }
}
