namespace FriendCore.Application.Common.Options;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>
    ///     S3-compatible endpoint URL. MinIO in dev/test (http://localhost:9000); the Cloudflare R2
    ///     account endpoint in prod. Storage services are not registered when this is empty.
    /// </summary>
    public string? Endpoint { get; init; }

    public string Bucket { get; init; } = "friendcore-media";

    public string? AccessKey { get; init; }

    public string? SecretKey { get; init; }

    /// <summary>R2 expects "auto"; MinIO ignores the value.</summary>
    public string Region { get; init; } = "auto";

    /// <summary>
    ///     Public origin that variant storage keys are appended to when building media URLs in
    ///     responses (the CDN/R2 public bucket domain in prod, the MinIO endpoint + bucket in dev).
    ///     Only variants are ever public — originals are served exclusively via presigned URLs.
    /// </summary>
    public string? PublicBaseUrl { get; init; }

    /// <summary>
    ///     Dev/test only: create the bucket on startup if missing. Keep false in prod — the R2
    ///     bucket is provisioned ahead of time and the API credentials need no bucket permissions.
    /// </summary>
    public bool EnsureBucketExists { get; init; }

    /// <summary>Spec §7.3: original-file access links expire after 15 minutes.</summary>
    public int PresignedUrlExpiryMinutes { get; init; } = 15;
}
