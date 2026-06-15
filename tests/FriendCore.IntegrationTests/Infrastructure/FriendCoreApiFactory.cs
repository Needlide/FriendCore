using System.Net;
using System.Threading.RateLimiting;
using FriendCore.API.Extensions;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Infrastructure.Caching;
using FriendCore.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Testcontainers.Minio;
using Testcontainers.PostgreSql;

namespace FriendCore.IntegrationTests.Infrastructure;

public sealed class FriendCoreApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string JwtSecret = "integration-tests-secret-key-that-is-long-enough-32+";
    private const string JwtIssuer = "FriendCore.Tests";
    private const string JwtAudience = "FriendCore.Tests";
    private const string StorageAccessKey = "minio";
    private const string StorageSecretKey = "minio-secret-key";
    private const string StorageBucket = "friendcore-media-tests";

    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    private readonly MinioContainer _storage = new MinioBuilder()
        .WithUsername(StorageAccessKey)
        .WithPassword(StorageSecretKey)
        .Build();

    public FriendCoreApiFactory()
    {
        // Program reads the Jwt section at top-level (to build the bearer validation parameters)
        // before ConfigureAppConfiguration runs, so these must be present as env vars — which
        // WebApplication.CreateBuilder picks up at builder-creation time — or the validator and the
        // token service end up with different secrets and every authenticated request 401s.
        Environment.SetEnvironmentVariable("Jwt__SecretKey", JwtSecret);
        Environment.SetEnvironmentVariable("Jwt__Issuer", JwtIssuer);
        Environment.SetEnvironmentVariable("Jwt__Audience", JwtAudience);
    }

    public CapturingEmailService Emails { get; } = new();

    public MutableDateTimeProvider Clock { get; } = new();

    async Task IAsyncLifetime.InitializeAsync()
    {
        await Task.WhenAll(_database.StartAsync(), _storage.StartAsync());

        // Accessing Services builds the host (running ConfigureWebHost above) so migrations target
        // the container.
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _database.DisposeAsync();
        await _storage.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] = _database.GetConnectionString(),
                ["Jwt:SecretKey"] = JwtSecret,
                ["Jwt:Issuer"] = JwtIssuer,
                ["Jwt:Audience"] = JwtAudience,
                ["Jwt:AccessTokenExpirationMinutes"] = "60",
                // Small batch so the auto-archive drain loop crosses multiple batches in tests.
                ["TagMaintenance:ArchiveBatchSize"] = "3",
                ["Storage:Endpoint"] = _storage.GetConnectionString(),
                ["Storage:Bucket"] = StorageBucket,
                ["Storage:AccessKey"] = StorageAccessKey,
                ["Storage:SecretKey"] = StorageSecretKey,
                ["Storage:PublicBaseUrl"] = $"{_storage.GetConnectionString()}/{StorageBucket}",
                // Exercises StorageBucketInitializer on every host start (and its idempotence).
                ["Storage:EnsureBucketExists"] = "true",
                // Fast sweeps so tests can observe restart recovery without waiting, but a real
                // stuck threshold: only items with a backdated CreatedAt are swept, so freshly
                // created Processing fixtures in other tests are never picked up mid-assertion.
                ["MediaProcessing:SweepIntervalSeconds"] = "1",
                ["MediaProcessing:StuckAfterMinutes"] = "30"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Capture emails instead of hitting Resend — this is how the raw token is recovered.
            services.RemoveAll<IEmailService>();
            services.AddSingleton<IEmailService>(Emails);

            // Controllable clock for expiry tests.
            services.RemoveAll<IDateTimeProvider>();
            services.AddSingleton<IDateTimeProvider>(Clock);

            // The in-memory TestServer leaves Connection.RemoteIpAddress null (real Kestrel always
            // sets it), so populate a loopback IP like a real server would — lets tests verify the
            // request-IP wiring (e.g. the §10.3 moderation audit log).
            services.AddSingleton<IStartupFilter, RemoteIpStartupFilter>();

            // No Redis in the test environment: use the no-op cache so cached paths
            // (e.g. tag autocomplete) fall through to the database.
            services.RemoveAll<ICacheService>();
            services.AddSingleton<ICacheService, NullCacheService>();

            // The test server gives every request the same partition key, so the real 10/min auth
            // limit would trip across tests. Replace the policy config with no-op limiters (the
            // rate-limiter core services stay registered from Program's AddApiRateLimiting call).
            services.RemoveAll<IConfigureOptions<RateLimiterOptions>>();
            services.Configure<RateLimiterOptions>(options =>
            {
                options.AddPolicy(RateLimitingExtensions.AuthPolicy, _ => RateLimitPartition.GetNoLimiter("test"));
                options.AddPolicy(RateLimitingExtensions.ApiPolicy, _ => RateLimitPartition.GetNoLimiter("test"));
            });
        });
    }

    // Stamps a loopback remote IP on every request, standing in for what Kestrel provides in
    // production (the in-memory TestServer otherwise leaves it null).
    private sealed class RemoteIpStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
            app =>
            {
                app.Use(async (context, nextMiddleware) =>
                {
                    context.Connection.RemoteIpAddress ??= IPAddress.Loopback;
                    await nextMiddleware();
                });
                next(app);
            };
    }
}