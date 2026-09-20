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

        // Media variants are served straight to browsers (never proxied through the API),
        // so the bucket needs public read. Dev/test only: this initializer never runs in
        // production (EnsureBucketExists off), where buckets are provisioned with proper policy.
        var policy = $$"""
            {"Version":"2012-10-17","Statement":[{"Effect":"Allow","Principal":{"AWS":["*"]},"Action":["s3:GetObject"],"Resource":["arn:aws:s3:::{{storage.Bucket}}/*"] }]}
            """;
        await client.PutBucketPolicyAsync(
            new PutBucketPolicyRequest { BucketName = storage.Bucket, Policy = policy },
            cancellationToken);
        logger.LogInformation("Applied public-read policy to storage bucket {Bucket}", storage.Bucket);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
