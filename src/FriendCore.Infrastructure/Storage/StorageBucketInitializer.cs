using Amazon.S3;
using Amazon.S3.Model;
using FriendCore.Application.Common.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FriendCore.Infrastructure.Storage;

/// <summary>
///     Dev/test convenience (registered only when StorageOptions.EnsureBucketExists): creates the
///     media bucket on startup so a fresh MinIO needs no manual setup. Prod buckets are provisioned
///     ahead of time and the API credentials deliberately lack bucket-management permissions.
/// </summary>
internal sealed class StorageBucketInitializer(
    IServiceProvider serviceProvider,
    IOptions<StorageOptions> options,
    ILogger<StorageBucketInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var storage = options.Value;
        if (!storage.EnsureBucketExists || string.IsNullOrWhiteSpace(storage.Endpoint))
            return;

        // Resolved here, not in the constructor, so a host without storage configured never
        // constructs the S3 client (which requires an endpoint).
        var client = serviceProvider.GetRequiredService<IAmazonS3>();

        try
        {
            await client.PutBucketAsync(new PutBucketRequest { BucketName = storage.Bucket }, cancellationToken);
            logger.LogInformation("Created storage bucket {Bucket}", storage.Bucket);
        }
        catch (AmazonS3Exception e) when
            (e.ErrorCode is "BucketAlreadyOwnedByYou" or "BucketAlreadyExists")
        {
            // Already provisioned — the normal case after first startup.
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
