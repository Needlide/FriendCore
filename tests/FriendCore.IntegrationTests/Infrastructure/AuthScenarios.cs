using System.Net.Http.Json;
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

    public static Task<HttpResponseMessage> Login(HttpClient client, string email, string password = DefaultPassword)
    {
        return client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
    }

    public static async Task<string> RegisterAndGetToken(HttpClient client, string email)
    {
        var registered = await Register(client, email);
        var body = await registered.Content.ReadFromJsonAsync<AuthResponse>();
        return body!.AccessToken;
    }

    public async Task AssertUser(string email, Action<User> assert)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);
        assert(user);
    }

    public async Task<Ulid> GetUserIdAsync(string email)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);
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
        var user = await db.Users.SingleAsync(u => u.Email == email);
        user.AssignRole(role);
        await db.SaveChangesAsync();
    }

    // Register, elevate the role in the DB, then log in so the issued token carries the new role.
    public async Task<string> RegisterWithRoleAndGetToken(HttpClient client, string email, UserRole role)
    {
        await Register(client, email);
        await PromoteUserAsync(email, role);

        var login = await Login(client, email);
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
        var user = await db.Users.SingleAsync(u => u.Email == email);
        assert(user.ProfileTags);
    }
}