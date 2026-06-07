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

        var registered = await AuthScenarios.Register(client, email);
        var registerBody = await registered.Content.ReadFromJsonAsync<AuthResponse>();

        var login = await AuthScenarios.Login(client, email);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var body = await login.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotEmpty(body!.AccessToken);
        Assert.Equal(registerBody!.UserId, body.UserId);
        Assert.False(body.IsEmailVerified);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_ReturnsUnauthorized()
    {
        factory.Clock.Reset();
        var client = factory.CreateClient();

        var login = await AuthScenarios.Login(client, AuthScenarios.UniqueEmail());

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        factory.Clock.Reset();
        var client = factory.CreateClient();
        var email = AuthScenarios.UniqueEmail();

        await AuthScenarios.Register(client, email);

        var login = await AuthScenarios.Login(client, email, "wrong-password");

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Login_ForBannedUser_ReturnsForbidden()
    {
        factory.Clock.Reset();
        var client = factory.CreateClient();
        var email = AuthScenarios.UniqueEmail();

        await AuthScenarios.Register(client, email);
        await _scenarios.BanUser(email);

        var login = await AuthScenarios.Login(client, email);

        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
    }

    [Theory]
    [InlineData("", "password123")]
    [InlineData("alice@example.com", "")]
    [InlineData("not-an-email", "password123")]
    public async Task Login_WithInvalidPayload_ReturnsBadRequest(string email, string password)
    {
        factory.Clock.Reset();
        var client = factory.CreateClient();

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });

        Assert.Equal(HttpStatusCode.BadRequest, login.StatusCode);
    }

    [Fact]
    public async Task Login_IssuesTokenAcceptedByAuthorizedEndpoint()
    {
        factory.Clock.Reset();
        var client = factory.CreateClient();
        var email = AuthScenarios.UniqueEmail();

        await AuthScenarios.Register(client, email);
        var login = await AuthScenarios.Login(client, email);
        var body = await login.Content.ReadFromJsonAsync<AuthResponse>();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        var resend = await client.PostAsync("/api/v1/auth/resend-verification", content: null);

        Assert.Equal(HttpStatusCode.NoContent, resend.StatusCode);
    }
}
