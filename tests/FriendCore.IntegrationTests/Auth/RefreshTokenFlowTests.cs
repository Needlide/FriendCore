using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FriendCore.Application.Common.Models;
using FriendCore.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FriendCore.IntegrationTests.Auth;

public class RefreshTokenFlowTests(FriendCoreApiFactory factory) : IClassFixture<FriendCoreApiFactory>
{
    private const string CookieName = "fc_refresh";
    private readonly AuthScenarios _scenarios = new(factory);

    // Cookies are driven manually so a test can replay a stale token after rotation; the client's
    // own cookie jar would silently overwrite the old value with the rotated one.
    private HttpClient CreateClient()
    {
        factory.Clock.Reset();
        return factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
    }

    private static async Task<string> LoginAndGetRefreshCookie(HttpClient client, string email)
    {
        var login = await AuthScenarios.Login(client, email);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return ReadRefreshCookie(login);
    }

    private static Task<HttpResponseMessage> Refresh(HttpClient client, string refreshToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        request.Headers.Add("Cookie", $"{CookieName}={refreshToken}");
        return client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> Logout(HttpClient client, string refreshToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        request.Headers.Add("Cookie", $"{CookieName}={refreshToken}");
        return client.SendAsync(request);
    }

    // Pulls the raw fc_refresh value out of the Set-Cookie header (everything before the first ';').
    private static string ReadRefreshCookie(HttpResponseMessage response)
    {
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies), "expected a Set-Cookie header");
        var setCookie = cookies!.Single(c => c.StartsWith($"{CookieName}=", StringComparison.Ordinal));
        var value = setCookie.Split(';')[0][$"{CookieName}=".Length..];
        return value;
    }

    private static string ReadRawSetCookie(HttpResponseMessage response)
    {
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies), "expected a Set-Cookie header");
        return cookies!.Single(c => c.StartsWith($"{CookieName}=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Login_SetsHttpOnlyRefreshCookieScopedToAuth()
    {
        var client = CreateClient();
        var email = AuthScenarios.UniqueEmail();
        await AuthScenarios.Register(client, email);

        var login = await AuthScenarios.Login(client, email);

        var setCookie = ReadRawSetCookie(login);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/v1/auth", setCookie, StringComparison.OrdinalIgnoreCase);
        // The raw refresh token must never appear in the JSON body.
        var body = await login.Content.ReadAsStringAsync();
        Assert.DoesNotContain(CookieName, body);
        Assert.DoesNotContain(ReadRefreshCookie(login), body);
    }

    [Fact]
    public async Task Refresh_WithValidCookie_RotatesCookieAndReturnsUsableAccessToken()
    {
        var client = CreateClient();
        var email = AuthScenarios.UniqueEmail();
        await AuthScenarios.Register(client, email);
        var first = await LoginAndGetRefreshCookie(client, email);

        var refresh = await Refresh(client, first);
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);

        var rotated = ReadRefreshCookie(refresh);
        Assert.NotEqual(first, rotated); // rotation issued a brand-new token

        var body = await refresh.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotEmpty(body!.AccessToken);

        // The freshly minted access token is accepted by an authorized endpoint.
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.AccessToken);
        var authorized = await client.PostAsync("/api/v1/auth/resend-verification", null);
        Assert.Equal(HttpStatusCode.NoContent, authorized.StatusCode);
    }

    [Fact]
    public async Task Refresh_ReusingRotatedCookie_RevokesWholeFamily()
    {
        var client = CreateClient();
        var email = AuthScenarios.UniqueEmail();
        await AuthScenarios.Register(client, email);
        var first = await LoginAndGetRefreshCookie(client, email);

        var refresh = await Refresh(client, first);
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var rotated = ReadRefreshCookie(refresh);

        // Replaying the already-rotated token is treated as theft → 401 and family revocation.
        var replay = await Refresh(client, first);
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);

        // The rotated (previously valid) token is now dead too — the family was revoked.
        var afterReuse = await Refresh(client, rotated);
        Assert.Equal(HttpStatusCode.Unauthorized, afterReuse.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithoutCookie_ReturnsUnauthorized()
    {
        var client = CreateClient();

        var refresh = await client.PostAsync("/api/v1/auth/refresh", null);

        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Logout_RevokesRefreshToken_AndClearsCookie()
    {
        var client = CreateClient();
        var email = AuthScenarios.UniqueEmail();
        await AuthScenarios.Register(client, email);
        var refreshToken = await LoginAndGetRefreshCookie(client, email);

        var logout = await Logout(client, refreshToken);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        // The clearing cookie has an empty value (deletion) ...
        Assert.Empty(ReadRefreshCookie(logout));

        // ... and the revoked token can no longer be refreshed.
        var refresh = await Refresh(client, refreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Refresh_ForBannedUser_ReturnsForbidden()
    {
        var client = CreateClient();
        var email = AuthScenarios.UniqueEmail();
        await AuthScenarios.Register(client, email);
        var refreshToken = await LoginAndGetRefreshCookie(client, email);

        await _scenarios.BanUser(email);

        var refresh = await Refresh(client, refreshToken);
        Assert.Equal(HttpStatusCode.Forbidden, refresh.StatusCode);
    }
}
