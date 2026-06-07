using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Options;
using FriendCore.Domain.Tags;
using FriendCore.Domain.Users;
using FriendCore.Infrastructure.Authentication;
using FriendCore.Infrastructure.BackgroundJobs;
using FriendCore.Infrastructure.Caching;
using FriendCore.Infrastructure.Common;
using FriendCore.Infrastructure.Email;
using FriendCore.Infrastructure.Persistence;
using FriendCore.Infrastructure.Persistence.Interceptors;
using FriendCore.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, HttpCurrentUserService>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IEmailVerificationTokenRepository, EmailVerificationTokenRepository>();
        services.AddScoped<ITagRepository, TagRepository>();
        services.AddScoped<IFollowRepository, FollowRepository>();
        services.AddHostedService<TagMaintenanceService>();
        services.AddScoped<IDomainEventDispatcher, MediatRDomainEventDispatcher>();
        services.AddSingleton<IEmailVerificationTokenHasher, EmailVerificationTokenHasher>();
        services.AddHttpClient<IEmailService, ResendEmailService>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddSingleton<AuditInterceptor>();
        services.AddSingleton<SoftDeleteInterceptor>();

        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            options.UseNpgsql(configuration.GetConnectionString("Database"));
            options.AddInterceptors(
                serviceProvider.GetRequiredService<AuditInterceptor>(),
                serviceProvider.GetRequiredService<SoftDeleteInterceptor>());
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

        return services;
    }
}