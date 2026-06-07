using System.Net.Http.Json;
using FriendCore.Domain.Users;
using FriendCore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FriendCore.IntegrationTests.Infrastructure;

public class AuthScenarios(FriendCoreApiFactory factory)
{
    public const string DefaultPassword = "password123";

    public static string NewUsername() => "u" + Ulid.NewUlid().ToString().ToLowerInvariant()[..20];

    public static string UniqueEmail() => $"user-{Ulid.NewUlid()}@example.com".ToLowerInvariant();

    public static Task<HttpResponseMessage> Register(
        HttpClient client,
        string email,
        string? username = null,
        string password = DefaultPassword)
    {
        return client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { username = username ?? NewUsername(), displayName = "Test User", email, password });
    }

    public static Task<HttpResponseMessage> Login(HttpClient client, string email, string password = DefaultPassword)
    {
        return client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
    }

    public async Task AssertUser(string email, Action<User> assert)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);
        assert(user);
    }

    // No domain ban API exists yet; the IsBanned setter is private. Flip it via
    // reflection then persist so the AccountBanned path stays covered. Replace
    // with a real User.Ban(...) call once the moderation phase adds one.
    public async Task BanUser(string email)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);
        typeof(User).GetProperty(nameof(User.IsBanned))!.SetValue(user, true);
        await db.SaveChangesAsync();
    }
}
