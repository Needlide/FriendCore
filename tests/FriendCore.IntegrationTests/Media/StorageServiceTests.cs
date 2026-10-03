using System.Net;
using System.Text;
using FriendCore.Application.Common.Interfaces;
using FriendCore.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace FriendCore.IntegrationTests.Media;

/// <summary>
///     Proves the S3 storage stack end-to-end against a real MinIO container: bucket creation on
///     startup (StorageBucketInitializer), upload, download, presigned access, and deletion. Later
///     media-upload flow tests build on this foundation.
/// </summary>
public class StorageServiceTests(FriendCoreApiFactory factory) : IClassFixture<FriendCoreApiFactory>
{
    private static readonly byte[] Payload = Encoding.UTF8.GetBytes("fake-webp-bytes-for-roundtrip");

    private IStorageService Storage => factory.Services.GetRequiredService<IStorageService>();

    [Fact]
    public async Task Upload_Download_RoundTripsContent()
    {
        const string key = "uploads/originals/test-user/roundtrip.webp";

        using var content = new MemoryStream(Payload);
        await Storage.UploadAsync(key, content, "image/webp");

        await using var downloaded = await Storage.DownloadAsync(key);
        using var buffer = new MemoryStream();
        await downloaded.CopyToAsync(buffer);

        Assert.Equal(Payload, buffer.ToArray());
    }

    [Fact]
    public async Task PresignedUrl_ServesObject_DirectlyFromStorage()
    {
        const string key = "uploads/originals/test-user/presigned.webp";

        using var content = new MemoryStream(Payload);
        await Storage.UploadAsync(key, content, "image/webp");

        var url = await Storage.GetPresignedDownloadUrlAsync(key);

        // Plain HttpClient (not the API test client): the object must be served by MinIO itself —
        // the API never serves binaries (spec §7.2).
        using var external = new HttpClient();
        var response = await external.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/webp", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Payload, await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Delete_RemovesObject()
    {
        const string key = "uploads/originals/test-user/deleted.webp";

        using var content = new MemoryStream(Payload);
        await Storage.UploadAsync(key, content, "image/webp");
        var url = await Storage.GetPresignedDownloadUrlAsync(key);

        await Storage.DeleteAsync(key);

        // The signature is still valid after deletion — only the object is gone.
        using var external = new HttpClient();
        var response = await external.GetAsync(url);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// The regression guard for a real finding: the dev bucket policy used to grant
    /// <c>s3:GetObject</c> on <c>bucket/*</c>, which made every ORIGINAL anonymously fetchable
    /// with no signature — verified live as HTTP 200 with the full file. That silently defeated
    /// §7.3, whose entire premise is that originals are reachable only through a 15-minute
    /// presigned URL. The policy is now scoped to uploads/variants/*.
    ///
    /// Both probes derive their URL from a real presigned one and differ ONLY in whether the
    /// signature is present, which isolates the variable that matters and avoids guessing at
    /// what PublicBaseUrl actually resolves to.
    /// </summary>
    [Theory]
    [InlineData("uploads/originals/test-user/unsigned-original.webp", HttpStatusCode.Forbidden)]
    [InlineData("uploads/variants/probe/medium.webp", HttpStatusCode.OK)]
    public async Task BucketPolicy_ScopesPublicReadToVariantsOnly(string key, HttpStatusCode expected)
    {
        using var content = new MemoryStream(Payload);
        await Storage.UploadAsync(key, content, "image/webp");

        // Strip the query string: same scheme, host and path, no signature. Plain HttpClient —
        // no auth header, because the point is that storage alone must refuse (or allow).
        var presigned = await Storage.GetPresignedDownloadUrlAsync(key);
        var unsigned = presigned.Split('?')[0];

        using var external = new HttpClient();
        var response = await external.GetAsync(unsigned);

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task PresignedUrl_StillServesAnOriginal_UnderTheScopedPolicy()
    {
        // The other half of the pair. A presigned request is authorized by its own signature
        // using the API credentials' permissions rather than by the bucket policy, so scoping
        // public read to variants must NOT break the planned "Download original" flow (§7.3).
        // Together with the test above: unsigned fails, signed succeeds.
        const string key = "uploads/originals/test-user/signed-original.webp";

        using var content = new MemoryStream(Payload);
        await Storage.UploadAsync(key, content, "image/webp");

        var url = await Storage.GetPresignedDownloadUrlAsync(key);

        using var external = new HttpClient();
        var response = await external.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Payload, await response.Content.ReadAsByteArrayAsync());
    }
}
