namespace FriendCore.Application.Common.Interfaces;

/// <summary>
///     Object storage for media files (S3-compatible: MinIO in dev/test, Cloudflare R2 in prod).
///     Keys are bucket-relative paths (spec §7.1), e.g. uploads/originals/{userId}/{imageId}.{ext};
///     callers compose keys and public URLs — the service never decides layout.
/// </summary>
public interface IStorageService
{
    /// <summary>Stores <paramref name="content" /> under <paramref name="key" />. The caller owns the stream.</summary>
    Task UploadAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Opens the object for reading. The caller must dispose the returned stream — it holds the
    ///     underlying HTTP connection. Throws if the key does not exist.
    /// </summary>
    Task<Stream> DownloadAsync(string key, CancellationToken cancellationToken = default);

    Task DeleteAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Time-limited GET URL for an object (expiry from <see cref="Options.StorageOptions" />).
    ///     This is how originals are served — they never get a permanent public URL (spec §7.3).
    /// </summary>
    Task<string> GetPresignedDownloadUrlAsync(string key, CancellationToken cancellationToken = default);
}
