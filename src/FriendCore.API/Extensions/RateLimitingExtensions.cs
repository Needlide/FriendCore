using System.Threading.RateLimiting;

namespace FriendCore.API.Extensions;

public static class RateLimitingExtensions
{
    public const string AuthPolicy = "auth";
    public const string ApiPolicy = "api";
    public const string MessagingPolicy = "messaging";

    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();

                await context.HttpContext.Response.WriteAsJsonAsync(
                    new { Code = "Common.RateLimited", Description = "Too many requests. Please try again later." },
                    cancellationToken);
            };

            options.AddPolicy(AuthPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    GetPartitionKey(httpContext),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));

            options.AddPolicy(ApiPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    GetPartitionKey(httpContext),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 120,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));

            // DMs get their own bucket: bursts are normal chat behavior (fast consecutive
            // sentences), sustained floods are not. Token bucket allows a burst of 10 and
            // refills ~12/min — a letter-per-message spammer hits the wall almost immediately.
            options.AddPolicy(MessagingPolicy, httpContext =>
                RateLimitPartition.GetTokenBucketLimiter(
                    GetPartitionKey(httpContext),
                    _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = 10,
                        TokensPerPeriod = 1,
                        ReplenishmentPeriod = TimeSpan.FromSeconds(5),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
        });

        return services;
    }

    private static string GetPartitionKey(HttpContext httpContext)
    {
        return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}