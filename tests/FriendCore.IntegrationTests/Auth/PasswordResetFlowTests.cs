using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FriendCore.Application.Common.Models;
using FriendCore.IntegrationTests.Infrastructure;

namespace FriendCore.IntegrationTests.Auth;

/// <summary>
///     Password reset under the email-hash design: the address is supplied per request and
///     matched against the stored hash — nothing is ever read back out. Unknown users and
///     mismatched emails get the same blind 204; only an exact match sends a link, and only
///     to the entered address.
/// </summary>
public sealed class PasswordResetFlowTests(FriendCoreApiFactory factory) : IClassFixture<FriendCoreApiFactory>
{
    private static readonly Regex UrlPattern = new(@"https?://\S+", RegexOptions.Compiled);

    [Fact]
    public async Task ForgotPassword_WithUnknownUser_ReturnsNoContent_AndSendsNothing()
    {
        factory.Clock.Reset();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/forgot-password",
            new { username = AuthScenarios.NewUsername(), email = AuthScenarios.UniqueEmail() });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task ForgotPassword_WithMismatchedEmail_ReturnsNoContent_AndSendsNothing()
    {
        factory.Clock.Reset();
        var client = factory.CreateClient();
        var username = AuthScenarios.NewUsername();
        await AuthScenarios.Register(client, AuthScenarios.UniqueEmail(), username);
        var otherEmail = AuthScenarios.UniqueEmail();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/forgot-password",
            new { username, email = otherEmail });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(factory.Emails.For(otherEmail));
    }

    [Fact]
    public async Task Reset_FullFlow_RotatesPassword()
    {
        factory.Clock.Reset();
        var client = factory.CreateClient();
        var email = AuthScenarios.UniqueEmail();
        var username = AuthScenarios.NewUsername();
        await AuthScenarios.Register(client, email, username);

        var forgot = await client.PostAsJsonAsync(
            "/api/v1/auth/forgot-password",
            new { username, email });
        Assert.Equal(HttpStatusCode.NoContent, forgot.StatusCode);

        var token = ExtractResetToken(email);

        var reset = await client.PostAsJsonAsync(
            "/api/v1/auth/reset-password",
            new { token, newPassword = "brand-new-password-1" });
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        // Old password is dead, new one works.
        var oldLogin = await AuthScenarios.LoginByUsername(client, username, AuthScenarios.DefaultPassword);
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);

        var newLogin = await AuthScenarios.LoginByUsername(client, username, "brand-new-password-1");
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);

        // Single use: replaying the token fails.
        var replay = await client.PostAsJsonAsync(
            "/api/v1/auth/reset-password",
            new { token, newPassword = "another-password-1" });
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
    }

    [Fact]
    public async Task Reset_WithUnknownToken_ReturnsBadRequest()
    {
        factory.Clock.Reset();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/reset-password",
            new { token = "this-token-was-never-issued", newPassword = "brand-new-password-1" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private string ExtractResetToken(string email)
    {
        var message = factory.Emails
            .For(email)
            .Last(m => m.Subject == "Reset your FriendCore password");

        var url = UrlPattern.Match(message.PlainTextBody ?? string.Empty).Value;
        var query = new Uri(url).Query.TrimStart('?');
        var raw = query.Split('&')
            .Select(pair => pair.Split('=', 2))
            .First(parts => parts[0] == "token")[1];

        return Uri.UnescapeDataString(raw);
    }
}
