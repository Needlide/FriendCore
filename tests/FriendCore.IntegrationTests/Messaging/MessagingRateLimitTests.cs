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

        // The bucket holds 10 and refills one token every 5 s. Do NOT assume a fixed number of
        // requests fills it: a slow CI run can earn a token mid-loop and turn the old "11th is
        // rejected" assertion into a flake. Keep sending until the limiter says stop instead —
        // 40 requests need >3 minutes of refill to all pass, far longer than this test takes.
        HttpResponseMessage? rejected = null;
        for (var i = 0; i < 40 && rejected is null; i++)
        {
            var send = await alice.PostAsJsonAsync(
                $"/api/v1/conversations/{conversation!.Id}/messages", new { content = $"m{i}" });

            // Anything other than success or the limiter's 429 is a real bug, not a flake.
            Assert.True(
                send.StatusCode is HttpStatusCode.Created or HttpStatusCode.TooManyRequests,
                $"Unexpected status {(int)send.StatusCode} on message {i}.");

            if (send.StatusCode == HttpStatusCode.TooManyRequests)
                rejected = send;
        }

        Assert.NotNull(rejected);
        Assert.NotNull(rejected!.Headers.RetryAfter);
        Assert.True(rejected.Headers.RetryAfter!.Delta > TimeSpan.Zero);
    }
}
