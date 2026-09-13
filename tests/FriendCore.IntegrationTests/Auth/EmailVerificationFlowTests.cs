using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Users;
using FriendCore.Infrastructure.Persistence;
using FriendCore.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FriendCore.IntegrationTests.Auth;

public sealed class EmailVerificationFlowTests(FriendCoreApiFactory factory) : IClassFixture<FriendCoreApiFactory>
{
    private static readonly Regex UrlPattern = new(@"https?://\S+", RegexOptions.Compiled);

    [Fact]
    public async Task RegisterThenVerify_MarksUserVerified_AndConsumesToken()
    {
        factory.Clock.Reset();
        var client = factory.CreateClient();
        var email = AuthScenarios.UniqueEmail();

        var registered = await AuthScenarios.Register(client, email);
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        var registerBody = await registered.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.False(registerBody!.IsEmailVerified);

        var token = ExtractVerificationToken(email);

        var verify = await client.PostAsJsonAsync("/api/v1/auth/verify-email", new { token });
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        var verifyBody = await verify.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.True(verifyBody!.IsEmailVerified);

        var authScenarios = new AuthScenarios(factory);

        await authScenarios.AssertUser(email, user => { Assert.True(user.IsVerified); });
        await AssertTokens(email, tokens =>
        {
            Assert.Single(tokens);
            Assert.NotNull(tokens[0].ConsumedAt);
        });
    }

    [Fact]
    public async Task Verify_WithConsumedToken_ReturnsBadRequest()
    {
        factory.Clock.Reset();
        var client = factory.CreateClient();
        var email = AuthScenarios.UniqueEmail();

        await AuthScenarios.Register(client, email);
        var token = ExtractVerificationToken(email);

        var first = await client.PostAsJsonAsync("/api/v1/auth/verify-email", new { token });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var replay = await client.PostAsJsonAsync("/api/v1/auth/verify-email", new { token });
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
    }

    [Fact]
    public async Task Verify_WithUnknownToken_ReturnsBadRequest()
    {
        factory.Clock.Reset();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/verify-email",
            new { token = "this-token-was-never-issued" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Verify_WithExpiredToken_ReturnsBadRequest()
    {
        var start = DateTimeOffset.UtcNow;
        factory.Clock.Set(start);
        var client = factory.CreateClient();
        var email = AuthScenarios.UniqueEmail();

        await AuthScenarios.Register(client, email);
        var token = ExtractVerificationToken(email);

        // Token validity is 24h; jump past it.
        factory.Clock.Set(start.AddHours(25));

        var response = await client.PostAsJsonAsync("/api/v1/auth/verify-email", new { token });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var authScenarios = new AuthScenarios(factory);

        await authScenarios.AssertUser(email, user => Assert.False(user.IsVerified));
        factory.Clock.Reset();
    }

    [Fact]
    public async Task Resend_RevokesPreviousTokenAndIssuesNew()
    {
        factory.Clock.Reset();
        var client = factory.CreateClient();
        var email = AuthScenarios.UniqueEmail();

        var registered = await AuthScenarios.Register(client, email);
        var registerBody = await registered.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", registerBody!.AccessToken);

        var resend = await client.PostAsJsonAsync("/api/v1/auth/resend-verification", new { email });
        Assert.Equal(HttpStatusCode.NoContent, resend.StatusCode);

        await AssertTokens(email, tokens =>
        {
            Assert.Equal(2, tokens.Count);
            Assert.Equal(1, tokens.Count(t => t.ConsumedAt is null));
        });

        var verificationEmails = factory.Emails
            .For(email)
            .Count(m => m.Subject == "Verify your FriendCore email");
        Assert.Equal(2, verificationEmails);
    }

    private string ExtractVerificationToken(string email)
    {
        var message = factory.Emails
            .For(email)
            .Last(m => m.Subject == "Verify your FriendCore email");

        var url = UrlPattern.Match(message.PlainTextBody ?? string.Empty).Value;
        var query = new Uri(url).Query.TrimStart('?');
        var raw = query.Split('&')
            .Select(pair => pair.Split('=', 2))
            .First(parts => parts[0] == "token")[1];

        return Uri.UnescapeDataString(raw);
    }

    private async Task AssertTokens(string email, Action<List<EmailVerificationToken>> assert)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.EmailHash == AuthScenarios.EmailHash(email));
        var tokens = await db.EmailVerificationTokens
            .Where(t => t.UserId == user.Id)
            .ToListAsync();
        assert(tokens);
    }
}