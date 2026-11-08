using System.Net;
using System.Net.Http.Json;
using FriendCore.Application.Common.Models;
using FriendCore.IntegrationTests.Infrastructure;

namespace FriendCore.IntegrationTests.Social;

/// <summary>
///     §3/§7 MVP: global user search matches username or display name, excludes the caller,
///     block-hidden accounts, and banned accounts, and needs at least two characters.
/// </summary>
public class SearchUsersFlowTests(FriendCoreApiFactory factory) : IClassFixture<FriendCoreApiFactory>
{
    private readonly AuthScenarios _scenarios = new(factory);

    private Task<(HttpClient Client, string Username)> RegisterClient() =>
        _scenarios.RegisterVerifiedClientAsync();

    private static async Task<UserSearchResponse> Search(HttpClient client, string query)
    {
        return (await client.GetFromJsonAsync<UserSearchResponse>(
            $"/api/v1/users/search?q={Uri.EscapeDataString(query)}"))!;
    }

    [Fact]
    public async Task Search_MatchesUsernameSubstring()
    {
        var (viewer, _) = await RegisterClient();
        var (target, targetName) = await RegisterClient();
        var needle = targetName[2..10];

        var result = await Search(viewer, needle);

        Assert.Contains(result.Users, user => user.Username == targetName);
    }

    [Fact]
    public async Task Search_MatchesDisplayName_CaseInsensitively()
    {
        var (viewer, _) = await RegisterClient();
        var (target, targetName) = await RegisterClient();
        var displayName = $"Zelena {Ulid.NewUlid().ToString()[..6]}";

        var update = await target.PatchAsJsonAsync(
            "/api/v1/users/me/profile", new { displayName });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var result = await Search(viewer, displayName.ToLowerInvariant());

        Assert.Contains(result.Users, user => user.Username == targetName);
    }

    [Fact]
    public async Task Search_ExcludesTheCaller()
    {
        var (viewer, viewerName) = await RegisterClient();

        var result = await Search(viewer, viewerName);

        Assert.DoesNotContain(result.Users, user => user.Username == viewerName);
    }

    [Fact]
    public async Task Search_ShortOrMissingQuery_ReturnsEmpty()
    {
        var (viewer, _) = await RegisterClient();

        Assert.Empty((await Search(viewer, "a")).Users);

        var missing = await viewer.GetFromJsonAsync<UserSearchResponse>("/api/v1/users/search");
        Assert.Empty(missing!.Users);
    }

    [Fact]
    public async Task Search_ExcludesBlockedUsers()
    {
        var (viewer, _) = await RegisterClient();
        var (_, targetName) = await RegisterClient();
        var needle = targetName[2..10];
        Assert.Contains((await Search(viewer, needle)).Users, user => user.Username == targetName);

        await viewer.PostAsync($"/api/v1/users/{targetName}/block", content: null);

        Assert.DoesNotContain((await Search(viewer, needle)).Users, user => user.Username == targetName);
    }

    [Fact]
    public async Task Search_IsAvailableToAnonymousVisitors()
    {
        var (_, targetName) = await RegisterClient();

        var anonymous = factory.CreateClient();
        var result = await Search(anonymous, targetName[2..10]);

        Assert.Contains(result.Users, user => user.Username == targetName);
    }
}
