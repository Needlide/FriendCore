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
}
