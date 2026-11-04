using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FriendCore.Application.Common.Models;
using FriendCore.IntegrationTests.Infrastructure;

namespace FriendCore.IntegrationTests.Auth;

public class UserLoginFlowTests(FriendCoreApiFactory factory) : IClassFixture<FriendCoreApiFactory>
{
    private readonly AuthScenarios _scenarios = new(factory);

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsOkAndToken()
    {
        factory.Clock.Reset();
        var client = factory.CreateClient();
        var email = AuthScenarios.UniqueEmail();
        var username = AuthScenarios.NewUsername();

        var registered = await AuthScenarios.Register(client, email, username);
        var registerBody = await registered.Content.ReadFromJsonAsync<AuthResponse>();

        var login = await AuthScenarios.LoginByUsername(client, username);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var body = await login.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotEmpty(body!.AccessToken);
        Assert.Equal(registerBody!.UserId, body.UserId);
        Assert.False(body.IsEmailVerified);
    }

    [Fact]
    public async Task Login_WithUnknownUsername_ReturnsUnauthorized()
    {
        factory.Clock.Reset();
        var client = factory.CreateClient();

        var login = await AuthScenarios.LoginByUsername(client, AuthScenarios.NewUsername());

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        factory.Clock.Reset();
        var client = factory.CreateClient();
        var email = AuthScenarios.UniqueEmail();
        var username = AuthScenarios.NewUsername();

        await AuthScenarios.Register(client, email, username);

        var login = await AuthScenarios.LoginByUsername(client, username, "wrong-password");

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Login_ForBannedUser_ReturnsForbidden()
    {
        factory.Clock.Reset();
        var client = factory.CreateClient();
        var email = AuthScenarios.UniqueEmail();
        var username = AuthScenarios.NewUsername();

        await AuthScenarios.Register(client, email, username);
        await _scenarios.BanUser(email);

        var login = await AuthScenarios.LoginByUsername(client, username);

        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
    }

    [Fact]
    public async Task Login_ForSuspendedUser_ReturnsForbidden()
    {
        factory.Clock.Reset();
        var client = factory.CreateClient();
        var email = AuthScenarios.UniqueEmail();
        var username = AuthScenarios.NewUsername();

        await AuthScenarios.Register(client, email, username);
        await _scenarios.SuspendUser(email, TimeSpan.FromDays(7));

        var login = await AuthScenarios.LoginByUsername(client, username);

        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
    }

    [Fact]
    public async Task Login_AfterSuspensionExpires_ReturnsOk()
    {
        factory.Clock.Reset();
        var client = factory.CreateClient();
        var email = AuthScenarios.UniqueEmail();
        var username = AuthScenarios.NewUsername();

        await AuthScenarios.Register(client, email, username);
        await _scenarios.SuspendUser(email, TimeSpan.FromDays(7));

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await AuthScenarios.LoginByUsername(client, username)).StatusCode);

        // Advance the controllable clock past SuspendedUntil — IsSuspended compares
        // against IDateTimeProvider.UtcNow, so login re-opens without any other change.
        try
        {
            factory.Clock.Set(factory.Clock.UtcNow.AddDays(8));

            var login = await AuthScenarios.LoginByUsername(client, username);
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        }
        finally
        {
            factory.Clock.Reset();
        }
    }

    [Theory]
    [InlineData("", "password123")]
    [InlineData("alice", "")]
    public async Task Login_WithInvalidPayload_ReturnsBadRequest(string username, string password)
    {
        factory.Clock.Reset();
        var client = factory.CreateClient();

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { username, password });

        Assert.Equal(HttpStatusCode.BadRequest, login.StatusCode);
    }

    [Fact]
    public async Task Login_IssuesTokenAcceptedByAuthorizedEndpoint()
    {
        factory.Clock.Reset();
        var client = factory.CreateClient();
        var email = AuthScenarios.UniqueEmail();
        var username = AuthScenarios.NewUsername();

        await AuthScenarios.Register(client, email, username);
        var login = await AuthScenarios.LoginByUsername(client, username);
        var body = await login.Content.ReadFromJsonAsync<AuthResponse>();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        var resend = await client.PostAsJsonAsync("/api/v1/auth/resend-verification", new { email });

        Assert.Equal(HttpStatusCode.NoContent, resend.StatusCode);
    }
}