using System.Net;
using System.Net.Http.Headers;
using FriendCore.Domain.Users;
using FriendCore.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace FriendCore.IntegrationTests.Security;

/// <summary>
///     Beta-blocking hardening: baseline security headers, and the §10.3 admin IP-allowlist
///     (404 for non-allowlisted IPs). The test host stamps a loopback client IP, so an allowlist
///     that excludes loopback must 404 admin routes, and one that includes it must let them through.
/// </summary>
public class SecurityHardeningTests(FriendCoreApiFactory factory) : IClassFixture<FriendCoreApiFactory>
{
    private async Task<string> ModeratorToken()
    {
        factory.Clock.Reset();
        return await new AuthScenarios(factory).RegisterWithRoleAndGetToken(
            factory.CreateClient(), AuthScenarios.UniqueEmail(), UserRole.Moderator);
    }

    private HttpClient ClientWithAdminAllowlist(string allowedIp)
    {
        var configured = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AdminAccess:AllowedIps:0"] = allowedIp
                })));
        return configured.CreateClient();
    }

    [Fact]
    public async Task SecurityHeaders_ArePresentOnResponses()
    {
        var response = await factory.CreateClient().GetAsync("/");

        Assert.True(response.Headers.TryGetValues("X-Content-Type-Options", out var nosniff));
        Assert.Equal("nosniff", Assert.Single(nosniff!));
        Assert.True(response.Headers.Contains("X-Frame-Options"));
        Assert.True(response.Headers.Contains("Referrer-Policy"));
    }

    [Fact]
    public async Task AdminRoute_Returns404_ForNonAllowlistedIp()
    {
        var token = await ModeratorToken();
        var client = ClientWithAdminAllowlist("203.0.113.99"); // not the test host's loopback IP
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/v1/admin/reports");

        // Hidden from a non-allowlisted IP even with a valid moderator token.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AdminRoute_Accessible_WhenIpIsAllowlisted()
    {
        var token = await ModeratorToken();
        var client = ClientWithAdminAllowlist("127.0.0.1"); // the test host's stamped loopback IP
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/v1/admin/reports");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AdminRoute_NotRestricted_WhenAllowlistEmpty()
    {
        // Base factory configures no allowlist → role-only gating, no IP 404.
        var token = await ModeratorToken();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/v1/admin/reports");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
