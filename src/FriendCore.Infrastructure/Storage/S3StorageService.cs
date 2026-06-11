using Amazon.S3;
using Amazon.S3.Model;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Options;
using Microsoft.Extensions.Options;

namespace FriendCore.Infrastructure.Storage;

internal sealed class S3StorageService(
    IAmazonS3 client,
    IOptions<StorageOptions> options,
    IDateTimeProvider dateTimeProvider) : IStorageService
{
    private readonly StorageOptions _options = options.Value;

    // The SDK presigns with https:// regardless of the endpoint scheme; plain-HTTP MinIO in
    // dev/test needs the protocol made explicit or the generated URL fails the TLS handshake.
    private readonly Protocol _presignProtocol =
        options.Value.Endpoint?.StartsWith("http://", StringComparison.OrdinalIgnoreCase) == true
            ? Protocol.HTTP
            : Protocol.HTTPS;

    public async Task UploadAsync(
        string key,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var request = new PutObjectRequest
        {
            BucketName = _options.Bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            // The caller owns the stream (it may be an inspected upload that is re-read).
            AutoCloseStream = false
        };

        await client.PutObjectAsync(request, cancellationToken);
    }

    public async Task<Stream> DownloadAsync(string key, CancellationToken cancellationToken = default)
    {
        var response = await client.GetObjectAsync(_options.Bucket, key, cancellationToken);

        // Disposing the returned stream releases the HTTP connection; the response object itself
        // holds nothing else unmanaged, so handing the stream to the caller is safe.
        return response.ResponseStream;
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        await client.DeleteObjectAsync(_options.Bucket, key, cancellationToken);
    }

    public async Task<string> GetPresignedDownloadUrlAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _options.Bucket,
            Key = key,
            Verb = HttpVerb.GET,
            Protocol = _presignProtocol,
            Expires = dateTimeProvider.UtcNow.UtcDateTime.AddMinutes(_options.PresignedUrlExpiryMinutes)
        };

        return await client.GetPreSignedURLAsync(request);
    }
}
