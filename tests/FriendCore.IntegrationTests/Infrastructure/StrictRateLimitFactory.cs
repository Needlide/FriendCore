namespace FriendCore.IntegrationTests.Infrastructure;

/// <summary>
///     Same host, but the REAL rate-limiter policies stay active (base factory bypasses them
///     because the test server shares one partition key). A separate fixture type so xUnit
///     builds an isolated host: only 429-focused classes use this.
/// </summary>
public sealed class StrictRateLimitFactory : FriendCoreApiFactory
{
    public StrictRateLimitFactory()
    {
        // Runs before IAsyncLifetime.InitializeAsync builds the host, so ConfigureWebHost
        // observes it in time — unlike setting the flag from a test-class constructor.
        BypassRateLimits = false;
    }
}
