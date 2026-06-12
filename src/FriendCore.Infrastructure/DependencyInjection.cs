using Amazon.Runtime;
using Amazon.S3;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Options;
using FriendCore.Domain.Discovery;
using FriendCore.Domain.Media;
using FriendCore.Domain.Posts;
using FriendCore.Domain.Tags;
using FriendCore.Domain.Users;
using FriendCore.Infrastructure.Authentication;
using FriendCore.Infrastructure.BackgroundJobs;
using FriendCore.Infrastructure.Caching;
using FriendCore.Infrastructure.Common;
using FriendCore.Infrastructure.Email;
using FriendCore.Infrastructure.MediaProcessing;
using FriendCore.Infrastructure.Persistence;
using FriendCore.Infrastructure.Persistence.Interceptors;
using FriendCore.Infrastructure.Persistence.Repositories;
using FriendCore.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace FriendCore.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        services.Configure<TagMaintenanceOptions>(configuration.GetSection(TagMaintenanceOptions.SectionName));
        services.Configure<MediaProcessingOptions>(configuration.GetSection(MediaProcessingOptions.SectionName));

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, HttpCurrentUserService>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IEmailVerificationTokenRepository, EmailVerificationTokenRepository>();
        services.AddScoped<ITagRepository, TagRepository>();
        services.AddScoped<IFollowRepository, FollowRepository>();
        services.AddScoped<IBlockRepository, BlockRepository>();
        services.AddScoped<IMuteRepository, MuteRepository>();
        services.AddScoped<IDiscoveryRepository, DiscoveryRepository>();
        services.AddScoped<IPostRepository, PostRepository>();
        services.AddScoped<IMediaRepository, MediaRepository>();
        services.AddHostedService<TagMaintenanceService>();

        // One concrete queue instance behind two faces: producers see IMediaProcessingQueue, the
        // consumer service reads the channel directly.
        services.AddSingleton<MediaProcessingQueue>();
        services.AddSingleton<IMediaProcessingQueue>(provider => provider.GetRequiredService<MediaProcessingQueue>());
        services.AddScoped<ProcessMediaJob>();
        services.AddHostedService<MediaProcessingService>();
        services.AddHostedService<MediaProcessingSweeper>();
        services.AddScoped<IDomainEventDispatcher, MediatRDomainEventDispatcher>();
        services.AddSingleton<IEmailVerificationTokenHasher, EmailVerificationTokenHasher>();
        services.AddHttpClient<IEmailService, ResendEmailService>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddSingleton<AuditInterceptor>();
        services.AddSingleton<SoftDeleteInterceptor>();

        var logSql = configuration.GetValue<bool>("Database:LogSql");

        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            options.UseNpgsql(configuration.GetConnectionString("Database"));
            options.AddInterceptors(
                serviceProvider.GetRequiredService<AuditInterceptor>(),
                serviceProvider.GetRequiredService<SoftDeleteInterceptor>());

            // Dev-only: dump the executed SQL (with parameter values) so queries can be copied
            // into EXPLAIN. EnableSensitiveDataLogging inlines parameters, so never enable in prod.
            if (logSql)
            {
                options.LogTo(Console.WriteLine, new[] { DbLoggerCategory.Database.Command.Name },
                    LogLevel.Information);
                options.EnableSensitiveDataLogging();
            }
        });

        var healthChecks = services.AddHealthChecks();

        var databaseConnection = configuration.GetConnectionString("Database");
        if (!string.IsNullOrWhiteSpace(databaseConnection))
            healthChecks.AddNpgSql(databaseConnection, name: "postgresql");

        var redisConnection = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redisConnection))
        {
            var redisOptions = ConfigurationOptions.Parse(redisConnection);
            // Keep startup resilient: a briefly-unavailable Redis must not crash the app or
            // block requests — the multiplexer reconnects in the background.
            redisOptions.AbortOnConnectFail = false;

            // Lazy: connects on first cache use, so paths that never touch the cache (e.g. tests)
            // don't open a connection.
            services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisOptions));
            services.AddSingleton<ICacheService, RedisCacheService>();

            healthChecks.AddRedis(redisConnection, "redis");
        }
        else
        {
            services.AddSingleton<ICacheService, NullCacheService>();
        }

        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));

        // Lazy like the Redis multiplexer — and config must be read at resolution time, not here:
        // registration-time reads run before WebApplicationFactory test overrides are applied.
        services.AddSingleton<IAmazonS3>(serviceProvider =>
        {
            var storage = serviceProvider.GetRequiredService<IOptions<StorageOptions>>().Value;
            if (string.IsNullOrWhiteSpace(storage.Endpoint))
                throw new InvalidOperationException(
                    "Storage:Endpoint is not configured — object storage is unavailable.");

            return new AmazonS3Client(
                new BasicAWSCredentials(storage.AccessKey, storage.SecretKey),
                new AmazonS3Config
                {
                    ServiceURL = storage.Endpoint,
                    // MinIO requires path-style addressing; R2 supports it too.
                    ForcePathStyle = true,
                    AuthenticationRegion = storage.Region,
                    // SDK v4 sends CRC32 integrity checksums by default, which MinIO/R2 reject on
                    // some operations — WHEN_REQUIRED restores the S3-compatible behaviour.
                    RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                    ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
                });
        });
        services.AddSingleton<IStorageService, S3StorageService>();
        services.AddHostedService<StorageBucketInitializer>();

        return services;
    }
}