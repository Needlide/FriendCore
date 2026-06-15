using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Users;
using FriendCore.IntegrationTests.Infrastructure;

namespace FriendCore.IntegrationTests.Moderation;

/// <summary>
///     Admin role management (POST /api/v1/admin/users/{username}/role): Admin-only, audited,
///     can't change your own role. Role takes effect on the target's next login (new token).
/// </summary>
public class RoleManagementFlowTests(FriendCoreApiFactory factory) : IClassFixture<FriendCoreApiFactory>
{
    private readonly AuthScenarios _scenarios = new(factory);

    private async Task<(HttpClient Client, string Username, string Email)> AdminClient()
    {
        factory.Clock.Reset();
        var client = factory.CreateClient();
        var email = AuthScenarios.UniqueEmail();
        var username = AuthScenarios.NewUsername();
        await AuthScenarios.Register(client, email, username);
        await _scenarios.PromoteUserAsync(email, UserRole.Admin);
        var login = await AuthScenarios.Login(client, email);
        var body = await login.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        return (client, username, email);
    }

    private async Task<(string Username, string Email)> RegisterPlainUser()
    {
        factory.Clock.Reset();
        var client = factory.CreateClient();
        var email = AuthScenarios.UniqueEmail();
        var username = AuthScenarios.NewUsername();
        await AuthScenarios.Register(client, email, username);
        return (username, email);
    }

    private static HttpClient ClientWithToken(FriendCoreApiFactory factory, string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task Admin_PromotesUser_WhoThenGainsModeratorAccessOnNextLogin()
    {
        var (admin, _, _) = await AdminClient();
        var (targetName, targetEmail) = await RegisterPlainUser();

        var promote = await admin.PostAsJsonAsync(
            $"/api/v1/admin/users/{targetName}/role", new { role = "Moderator", reason = "trusted" });
        Assert.Equal(HttpStatusCode.NoContent, promote.StatusCode);

        // Account view reflects the new role immediately (persisted).
        var account = await admin.GetFromJsonAsync<UserAccountResponse>($"/api/v1/admin/users/{targetName}");
        Assert.Equal("Moderator", account!.Role);

        // The role takes effect in authorization on the target's NEXT login (fresh token claim).
        var login = await AuthScenarios.Login(factory.CreateClient(), targetEmail);
        var token = (await login.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
        var promoted = ClientWithToken(factory, token);

        var queue = await promoted.GetAsync("/api/v1/admin/reports");
        Assert.Equal(HttpStatusCode.OK, queue.StatusCode);

        // The change is audited.
        var log = await admin.GetFromJsonAsync<AuditLogResponse>("/api/v1/admin/audit-log");
        Assert.Contains(log!.Entries, e => e.Action == "RoleChanged" && e.Reason == "trusted");
    }

    [Fact]
    public async Task Admin_CannotChangeOwnRole()
    {
        var (admin, adminName, _) = await AdminClient();

        var response = await admin.PostAsJsonAsync(
            $"/api/v1/admin/users/{adminName}/role", new { role = "User", reason = "oops" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Moderation.CannotChangeOwnRole", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Moderator_CannotManageRoles()
    {
        // Role management is Admin-only — a Moderator (who can reach other /admin routes) is forbidden.
        factory.Clock.Reset();
        var modClient = factory.CreateClient();
        var modToken = await _scenarios.RegisterWithRoleAndGetToken(
            modClient, AuthScenarios.UniqueEmail(), UserRole.Moderator);
        modClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", modToken);
        var (targetName, _) = await RegisterPlainUser();

        var response = await modClient.PostAsJsonAsync(
            $"/api/v1/admin/users/{targetName}/role", new { role = "Moderator", reason = "nope" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ChangeRole_WithoutReason_ReturnsBadRequest()
    {
        var (admin, _, _) = await AdminClient();
        var (targetName, _) = await RegisterPlainUser();

        var response = await admin.PostAsJsonAsync(
            $"/api/v1/admin/users/{targetName}/role", new { role = "Moderator", reason = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChangeRole_RequiresAuth()
    {
        var (targetName, _) = await RegisterPlainUser();

        var response = await factory.CreateClient().PostAsJsonAsync(
            $"/api/v1/admin/users/{targetName}/role", new { role = "Moderator", reason = "x" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
