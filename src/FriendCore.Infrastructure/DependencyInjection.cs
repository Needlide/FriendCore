using FriendCore.Application.Common.Interfaces;
using FriendCore.Domain.Users;
using FriendCore.Infrastructure.Authentication;
using FriendCore.Infrastructure.Common;
using FriendCore.Infrastructure.Persistence;
using FriendCore.Infrastructure.Persistence.Interceptors;
using FriendCore.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FriendCore.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        services.AddScoped<IUserRepository, UserRepository>();
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
            healthChecks.AddRedis(redisConnection, "redis");

        return services;
    }
}