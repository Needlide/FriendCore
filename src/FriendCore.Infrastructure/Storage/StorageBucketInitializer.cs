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

        // Media variants are served straight to browsers (never proxied through the API), so the
        // bucket needs public read — but ONLY on the variants prefix.
        //
        // Scoping to the whole bucket (bucket/*) silently defeats §7.3. Originals live in the
        // SAME bucket at uploads/originals/, and a bucket-wide policy makes every original
        // anonymously fetchable with no signature, making the 15-minute presigned control purely
        // decorative. Verified against dev: an unauthenticated GET of an original returned 200
        // and the full file while the policy read bucket/*.
        //
        // Presigned URLs are unaffected by this policy — a presigned request is authorized by its
        // own signature using the API credentials' permissions, not by the bucket policy — so the
        // planned "Download original" flow (§7.3) keeps working once it is built.
        //
        // Prod buckets must be provisioned with this same prefix-scoped policy. See the storage
        // section of docs/claude.md; there is no IaC in this repo, so it is a manual step and the
        // easiest thing to get wrong.
        var policy = $$"""
            {"Version":"2012-10-17","Statement":[{"Effect":"Allow","Principal":{"AWS":["*"]},"Action":["s3:GetObject"],"Resource":["arn:aws:s3:::{{storage.Bucket}}/uploads/variants/*"] }]}
            """;

        try
        {
            await client.PutBucketPolicyAsync(
                new PutBucketPolicyRequest { BucketName = storage.Bucket, Policy = policy },
                cancellationToken);
            logger.LogInformation(
                "Applied public-read policy to {Bucket}/uploads/variants/* (originals excluded)", storage.Bucket);
        }
        catch (AmazonS3Exception e) when (e.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            // Fail soft. This used to propagate: a credential without bucket-management rights
            // (exactly the prod posture the class comment describes) would crash the host at
            // startup. Losing the policy write should never be a reason the API will not boot.
            logger.LogWarning(
                e,
                "Could not apply the storage bucket policy — the credentials lack bucket-management " +
                "permissions. Expected in production, where the bucket is provisioned ahead of time. " +
                "Verify manually that public read is scoped to uploads/variants/* and NOT the whole bucket.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
