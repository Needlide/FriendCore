using System.Net;
using System.Net.Http.Json;
using FriendCore.Application.Common.Models;
using FriendCore.IntegrationTests.Infrastructure;

namespace FriendCore.IntegrationTests.Auth;

public class UserRegistrationFlowTests(FriendCoreApiFactory factory) : IClassFixture<FriendCoreApiFactory>
{
    private readonly AuthScenarios _scenarios = new(factory);

    [Fact]
    public async Task Register_WithValidPayload_PersistsHashedUserAndIssuesToken()
    {
        factory.Clock.Reset();
        var client = factory.CreateClient();
        var email = AuthScenarios.UniqueEmail();
        var username = AuthScenarios.NewUsername();

        var registered = await AuthScenarios.Register(client, email, username);

        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);

        var body = await registered.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotEmpty(body!.AccessToken);
        Assert.False(body.IsEmailVerified);
        Assert.Equal(username, body.Username);
        Assert.NotEmpty(body.UserId);

        await _scenarios.AssertUser(email, user =>
        {
            Assert.Equal(email, user.Email);
            Assert.NotEqual(AuthScenarios.DefaultPassword, user.PasswordHash);
            Assert.False(user.IsVerified);
        });
    }

    [Fact]
    public async Task Register_NormalizesEmailToLowercase()
    {
        factory.Clock.Reset();
        var client = factory.CreateClient();
        var email = AuthScenarios.UniqueEmail();
        var mixedCase = email.ToUpperInvariant();

        var registered = await AuthScenarios.Register(client, mixedCase);
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);

        await _scenarios.AssertUser(email, user => Assert.Equal(email, user.Email));
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsConflict()
    {
        factory.Clock.Reset();
        var client = factory.CreateClient();
        var email = AuthScenarios.UniqueEmail();

        var first = await AuthScenarios.Register(client, email);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await AuthScenarios.Register(client, email);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        var body = await second.Content.ReadAsStringAsync();
        Assert.Contains("User.EmailAlreadyInUse", body);
    }

    [Fact]
    public async Task Register_WithDuplicateUsername_ReturnsConflict()
    {
        factory.Clock.Reset();
        var client = factory.CreateClient();
        var username = AuthScenarios.NewUsername();

        var first = await AuthScenarios.Register(client, AuthScenarios.UniqueEmail(), username);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await AuthScenarios.Register(client, AuthScenarios.UniqueEmail(), username);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        var body = await second.Content.ReadAsStringAsync();
        Assert.Contains("User.UsernameAlreadyInUse", body);
    }

    [Theory]
    [InlineData("ab", "User.Username.TooShort")]
    [InlineData("not valid", "User.Username.InvalidFormat")]
    public async Task Register_WithInvalidUsername_ReturnsBadRequest(string username, string expectedCode)
    {
        factory.Clock.Reset();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new
            {
                username,
                displayName = "Test User",
                email = AuthScenarios.UniqueEmail(),
                password = AuthScenarios.DefaultPassword
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(expectedCode, await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("not-an-email", "password123", "User.Email.InvalidFormat")]
    [InlineData("valid@example.com", "short", "User.Password.TooShort")]
    public async Task Register_WithInvalidEmailOrPassword_ReturnsBadRequest(
        string email,
        string password,
        string expectedCode)
    {
        factory.Clock.Reset();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { username = AuthScenarios.NewUsername(), displayName = "Test User", email, password });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(expectedCode, await response.Content.ReadAsStringAsync());
    }
}
