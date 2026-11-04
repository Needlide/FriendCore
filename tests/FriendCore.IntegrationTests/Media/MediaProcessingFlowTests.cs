using System.Net.Http.Headers;
using System.Net.Http.Json;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Media;
using FriendCore.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using SixLabors.ImageSharp;

namespace FriendCore.IntegrationTests.Media;

/// <summary>
///     End-to-end §7.1 pipeline against real MinIO + Postgres: upload → background processing →
///     WebP variants in storage, plus the failure path and sweeper restart-recovery.
/// </summary>
public class MediaProcessingFlowTests(FriendCoreApiFactory factory) : IClassFixture<FriendCoreApiFactory>
{
    private async Task<HttpClient> AuthedClient() =>
        (await new AuthScenarios(factory).RegisterVerifiedClientAsync()).Client;

    private async Task<string> UploadAndAwait(HttpClient client, byte[] bytes, string expectedStatus)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        var form = new MultipartFormDataContent { { file, "file", "image.png" } };

        var response = await client.PostAsync("/api/v1/media", form);
        var body = await response.Content.ReadFromJsonAsync<MediaStatusResponse>();

        Assert.Equal(expectedStatus, await MediaUploadFlowTests.PollStatusAsync(client, body!.Id));
        return body.Id;
    }

    private async Task<MediaItem> LoadMedia(string id)
    {
        using var scope = factory.Services.CreateScope();
        var media = await scope.ServiceProvider.GetRequiredService<IMediaRepository>()
            .GetByIdAsync(Ulid.Parse(id));
        Assert.NotNull(media);
        return media;
    }

    [Fact]
    public async Task Processing_GeneratesAllVariants_WithCorrectDimensions()
    {
        var client = await AuthedClient();

        var id = await UploadAndAwait(client, TestImages.RealPng(1000, 600), "Ready");

        var media = await LoadMedia(id);
        Assert.Equal(5, media.Variants.Count);

        var byType = media.Variants.ToDictionary(v => v.Type);
        Assert.Equal((100, 100), (byType[MediaVariantType.Thumbnail].WidthPx, byType[MediaVariantType.Thumbnail].HeightPx));
        Assert.Equal((300, 180), (byType[MediaVariantType.Small].WidthPx, byType[MediaVariantType.Small].HeightPx));
        Assert.Equal((800, 480), (byType[MediaVariantType.Medium].WidthPx, byType[MediaVariantType.Medium].HeightPx));
        // Large caps at the 1000px source — no upscaling to 1400.
        Assert.Equal((1000, 600), (byType[MediaVariantType.Large].WidthPx, byType[MediaVariantType.Large].HeightPx));
        // The Original entry points at the preserved original's own key.
        Assert.Equal(media.OriginalStorageKey, byType[MediaVariantType.Original].StorageKey);
        Assert.Equal($"uploads/variants/{media.Id}/medium.webp", byType[MediaVariantType.Medium].StorageKey);
    }

    [Fact]
    public async Task Variants_AreWebp_AndStrippedOfExif()
    {
        var client = await AuthedClient();

        // The source PNG carries an EXIF profile (see TestImages.RealPng).
        var id = await UploadAndAwait(client, TestImages.RealPng(400, 400), "Ready");
        var media = await LoadMedia(id);

        var thumbnailKey = media.Variants.Single(v => v.Type == MediaVariantType.Thumbnail).StorageKey;
        await using var stored = await factory.Services.GetRequiredService<IStorageService>()
            .DownloadAsync(thumbnailKey);
        using var buffer = new MemoryStream();
        await stored.CopyToAsync(buffer);
        buffer.Position = 0;

        using var thumbnail = await Image.LoadAsync(buffer);
        Assert.Equal("image/webp", thumbnail.Metadata.DecodedImageFormat?.DefaultMimeType);
        Assert.Equal((100, 100), (thumbnail.Width, thumbnail.Height));
        Assert.Null(thumbnail.Metadata.ExifProfile);
    }

    [Fact]
    public async Task UndecodableUpload_BecomesFailed()
    {
        var client = await AuthedClient();

        // Valid PNG magic bytes (passes the upload header check) but undecodable garbage after.
        byte[] corrupt = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0xDE, 0xAD, 0xBE, 0xEF, 0x00, 0x01];

        var id = await UploadAndAwait(client, corrupt, "Failed");

        var media = await LoadMedia(id);
        Assert.Empty(media.Variants);
    }

    [Fact]
    public async Task Sweeper_RecoversItemsStuckInProcessing()
    {
        // Simulates an enqueue lost to a restart: the item exists in Processing with its original
        // in storage, but nothing ever entered the in-memory queue. Backdated past the stuck
        // threshold, the sweeper must pick it up and drive it to Ready.
        var media = MediaItem.Create(
            Ulid.NewUlid(), "stuck.png", "image/png", "png", MediaSafetyFlags.Empty,
            DateTimeOffset.UtcNow.AddHours(-1));

        using var content = new MemoryStream(TestImages.RealPng(50, 40));
        await factory.Services.GetRequiredService<IStorageService>()
            .UploadAsync(media.OriginalStorageKey, content, "image/png");

        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IMediaRepository>().AddAsync(media);
        }

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);
        var status = MediaStatus.Processing;
        while (status == MediaStatus.Processing && DateTime.UtcNow < deadline)
        {
            await Task.Delay(200);
            status = (await LoadMedia(media.Id.ToString())).Status;
        }

        Assert.Equal(MediaStatus.Ready, status);
    }
}
