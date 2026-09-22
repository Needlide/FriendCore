using System.Net;
using System.Net.Http.Json;
using FriendCore.Application.Common.Models;
using FriendCore.IntegrationTests.Infrastructure;

namespace FriendCore.IntegrationTests.Messaging;

/// <summary>
///     The messaging token bucket (burst 10, ~12/min refill) with the REAL limiter active
///     (BypassRateLimits=false — every other class bypasses, so this class is isolated).
///     Normal chat bursts pass; a letter-per-message flood hits 429 almost immediately.
/// </summary>
public class MessagingRateLimitTests : IClassFixture<StrictRateLimitFactory>
{
    private readonly StrictRateLimitFactory _factory;

    public MessagingRateLimitTests(StrictRateLimitFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task BurstBeyondTen_IsRejected_WithRetryAfter()
    {
        var (alice, _) = await new AuthScenarios(_factory).RegisterVerifiedClientAsync();
        var (_, bobName) = await new AuthScenarios(_factory).RegisterVerifiedClientAsync();
        var start = await alice.PostAsJsonAsync("/api/v1/conversations", new { username = bobName });
        var conversation = await start.Content.ReadFromJsonAsync<ConversationResponse>();

        // 1 start + 9 sends fill the bucket of 10; the 10th send is rejected.
        for (var i = 0; i < 9; i++)
        {
            var send = await alice.PostAsJsonAsync(
                $"/api/v1/conversations/{conversation!.Id}/messages", new { content = $"m{i}" });
            Assert.Equal(HttpStatusCode.Created, send.StatusCode);
        }

        var rejected = await alice.PostAsJsonAsync(
            $"/api/v1/conversations/{conversation!.Id}/messages", new { content = "spam" });
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.NotNull(rejected.Headers.RetryAfter);
    }
}
