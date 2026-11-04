using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Tags;
using FriendCore.Domain.Users;
using FriendCore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FriendCore.IntegrationTests.Infrastructure;

public class AuthScenarios(FriendCoreApiFactory factory)
{
    public const string DefaultPassword = "password123";

    public static string NewUsername()
    {
        return "u" + Ulid.NewUlid().ToString().ToLowerInvariant()[..20];
    }

    public static string UniqueEmail()
    {
        return $"user-{Ulid.NewUlid()}@example.com".ToLowerInvariant();
    }

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

    /// <summary>
    ///     Test-only HMAC-SHA256 with the factory pepper — mirrors HmacEmailHashService so
    ///     helpers can resolve users by the stored hash (plaintext is never persisted).
    /// </summary>
    public static string EmailHash(string email)
    {
        var hash = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes("integration-tests-email-pepper"),
            Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant()));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>Direct username login (what the API expects).</summary>
    public static Task<HttpResponseMessage> LoginByUsername(
        HttpClient client,
        string username,
        string password = DefaultPassword)
    {
        return client.PostAsJsonAsync("/api/v1/auth/login", new { username, password });
    }

    /// <summary>
    ///     Login by email for convenience: resolves the username from the stored hash, then
    ///     posts the username login the API expects.
    /// </summary>
    public async Task<HttpResponseMessage> LoginAsync(
        HttpClient client,
        string email,
        string password = DefaultPassword)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.EmailHash == EmailHash(email));
        return await client.PostAsJsonAsync("/api/v1/auth/login", new { username = user.Username, password });
    }

    public static async Task<string> RegisterAndGetToken(HttpClient client, string email)
    {
        var registered = await Register(client, email);
        var body = await registered.Content.ReadFromJsonAsync<AuthResponse>();
        return body!.AccessToken;
    }

    /// <summary>
    ///     Registers a fresh user, marks the email verified directly in the DB (the end-to-end
    ///     verification flow keeps its own coverage in EmailVerificationFlowTests), and returns
    ///     an authenticated client. Verified-by-default so the §13.7 posting gate doesn't ripple
    ///     through every flow test.
    /// </summary>
    public async Task<(HttpClient Client, string Username)> RegisterVerifiedClientAsync(string? username = null)
    {
        factory.Clock.Reset();
        var client = factory.CreateClient();
        var email = UniqueEmail();
        var name = username ?? NewUsername();
        await Register(client, email, name);
        await MarkVerifiedAsync(email);
        var login = await LoginAsync(client, email);
        var body = await login.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        return (client, name);
    }

    public async Task MarkVerifiedAsync(string email)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.EmailHash == EmailHash(email));
        user.MarkEmailVerified(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();
    }

    public async Task AssertUser(string email, Action<User> assert)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.EmailHash == EmailHash(email));
        assert(user);
    }

    public async Task<Ulid> GetUserIdAsync(string email)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.EmailHash == EmailHash(email));
        return user.Id;
    }

    // Insert posts that all share the exact same CreatedAt (distinct Ulids) via raw SQL — the
    // AuditInterceptor would otherwise stamp CreatedAt = UtcNow and we could never force a tie.
    // Returns the generated ids so a keyset test can assert ordering by the Id tiebreaker.
    public async Task<IReadOnlyList<Ulid>> SeedPostsWithSameTimestampAsync(
        Ulid authorId,
        int count,
        DateTimeOffset createdAt)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var ids = new List<Ulid>(count);
        for (var i = 0; i < count; i++)
        {
            var id = Ulid.NewUlid();
            ids.Add(id);
            await db.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO posts (id, author_id, content, sensitivity, like_count, comment_count, updated_at, deleted_at, created_at)
VALUES ({id.ToString()}, {authorId.ToString()}, {$"tied post {i}"}, {"General"}, 0, 0, {createdAt}, NULL, {createdAt})");
        }

        return ids;
    }

    public async Task BanUser(string email)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.EmailHash == EmailHash(email));
        user.Ban(factory.Clock.UtcNow);
        await db.SaveChangesAsync();
    }

    public async Task SuspendUser(string email, TimeSpan duration)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.EmailHash == EmailHash(email));
        var now = factory.Clock.UtcNow;
        user.Suspend(now.Add(duration), now);
        await db.SaveChangesAsync();
    }

    // No tag-moderation endpoint yet; approve directly so autocomplete (Active-only) can be exercised.
    public async Task ApproveTagAsync(string slug)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tag = await db.Tags.SingleAsync(t => t.Slug == slug);
        tag.Approve();
        await db.SaveChangesAsync();
    }

    public async Task PromoteUserAsync(string email, UserRole role)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.EmailHash == EmailHash(email));
        user.AssignRole(role);
        await db.SaveChangesAsync();
    }

    // Register, elevate the role in the DB, then log in so the issued token carries the new role.
    public async Task<string> RegisterWithRoleAndGetToken(HttpClient client, string email, UserRole role)
    {
        await Register(client, email);
        await PromoteUserAsync(email, role);

        var login = await LoginAsync(client, email);
        var body = await login.Content.ReadFromJsonAsync<AuthResponse>();
        return body!.AccessToken;
    }

    // Seed an Active tag that is already stale (idle far past the 180-day window, no users).
    public async Task SeedStaleActiveTagAsync(string name, string slug)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tag = Tag.Create(name, slug, Ulid.NewUlid(), DateTimeOffset.UtcNow.AddDays(-200));
        tag.Approve();
        db.Tags.Add(tag);
        await db.SaveChangesAsync();
    }

    // Seed several stale Active tags in one round-trip; returns their slugs.
    public async Task<IReadOnlyList<string>> SeedStaleActiveTagsAsync(int count, string slugPrefix)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var slugs = new List<string>(count);
        for (var i = 0; i < count; i++)
        {
            var slug = $"{slugPrefix}-{i}-{Ulid.NewUlid().ToString().ToLowerInvariant()[..8]}";
            var tag = Tag.Create($"Stale {i}", slug, Ulid.NewUlid(), DateTimeOffset.UtcNow.AddDays(-200));
            tag.Approve();
            db.Tags.Add(tag);
            slugs.Add(slug);
        }

        await db.SaveChangesAsync();
        return slugs;
    }

    public async Task AssertTag(string slug, Action<Tag> assert)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tag = await db.Tags.SingleAsync(t => t.Slug == slug);
        assert(tag);
    }

    public async Task AssertProfileTags(string email, Action<IReadOnlyList<ProfileTag>> assert)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.EmailHash == EmailHash(email));
        assert(user.ProfileTags);
    }
}