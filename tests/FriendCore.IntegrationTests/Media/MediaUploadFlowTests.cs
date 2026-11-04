using System.Security.Cryptography;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Media;
using FriendCore.Infrastructure.Persistence;
using FriendCore.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FriendCore.IntegrationTests.Media;

public class MediaUploadFlowTests(FriendCoreApiFactory factory) : IClassFixture<FriendCoreApiFactory>
{
    private static readonly byte[] PngBytes =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52];

    private static readonly byte[] JpegBytes =
        [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0x01, 0x00, 0x00, 0x01];

    private async Task<HttpClient> AuthedClient() =>
        (await new AuthScenarios(factory).RegisterVerifiedClientAsync()).Client;

    private static MultipartFormDataContent Upload(
        byte[] bytes,
        string contentType,
        bool hasFlashingWarning = false) =>
        UploadWithFlags(bytes, contentType, JsonSerializer.Serialize(new Dictionary<string, bool>
        {
            ["flashing"] = hasFlashingWarning,
            ["intenseMotion"] = false
        }));

    /// <summary>
    ///     Builds the form with a raw <c>SafetyFlags</c> value, so a test can send malformed JSON or
    ///     omit the field entirely. A null value omits it, which is the normal "nothing declared" case.
    /// </summary>
    private static MultipartFormDataContent UploadWithFlags(
        byte[] bytes,
        string contentType,
        string? safetyFlagsJson)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        var form = new MultipartFormDataContent { { file, "file", "test-image.bin" } };
        if (safetyFlagsJson is not null)
            form.Add(new StringContent(safetyFlagsJson), "SafetyFlags");
        return form;
    }

    [Fact]
    public async Task Upload_StoresOriginalInMinio_AndBecomesReady()
    {
        var client = await AuthedClient();
        var pngBytes = TestImages.RealPng(40, 30);

        var response = await client.PostAsync("/api/v1/media", Upload(pngBytes, "image/png", true));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<MediaStatusResponse>();
        // The upload response itself always reports Processing (spec §7.1 step 6); the background
        // pipeline then takes it to Ready, observed via status polling.
        Assert.Equal("Processing", body!.Status);
        Assert.Equal("Ready", await PollStatusAsync(client, body.Id));

        // The original is genuinely in object storage under the entity's key, byte-for-byte.
        using var scope = factory.Services.CreateScope();
        var media = await scope.ServiceProvider.GetRequiredService<IMediaRepository>()
            .GetByIdAsync(Ulid.Parse(body.Id));
        Assert.NotNull(media);
        Assert.True(media.SafetyFlags.IsSet("flashing"));

        await using var stored = await factory.Services.GetRequiredService<IStorageService>()
            .DownloadAsync(media.OriginalStorageKey);
        using var buffer = new MemoryStream();
        await stored.CopyToAsync(buffer);
        Assert.Equal(pngBytes, buffer.ToArray());
    }

    /// <summary>Polls the status endpoint until the item leaves Processing (or times out).</summary>
    internal static async Task<string> PollStatusAsync(HttpClient client, string id)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);
        var status = "Processing";

        while (status == "Processing" && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100);
            var response = await client.GetFromJsonAsync<MediaStatusResponse>($"/api/v1/media/{id}/status");
            status = response!.Status;
        }

        return status;
    }

    [Fact]
    public async Task Upload_WithMismatchedHeader_ReturnsBadRequest()
    {
        var client = await AuthedClient();

        // Declared PNG, actual JPEG bytes.
        var response = await client.PostAsync("/api/v1/media", Upload(JpegBytes, "image/png"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Media.HeaderMismatch", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Upload_WithUnsupportedType_ReturnsBadRequest()
    {
        var client = await AuthedClient();

        var response = await client.PostAsync("/api/v1/media", Upload("hello"u8.ToArray(), "text/plain"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Media.UnsupportedType", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Upload_WithMalformedSafetyFlags_ReturnsBadRequest()
    {
        var client = await AuthedClient();

        // A typo in the declaration must fail loudly: silently treating it as "nothing declared"
        // would drop a hazard the creator explicitly tried to report.
        var response = await client.PostAsync(
            "/api/v1/media",
            UploadWithFlags(TestImages.RealPng(20, 20), "image/png", "{not json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Media.InvalidSafetyFlags", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Upload_WithoutSafetyFlags_DefaultsToNone()
    {
        var client = await AuthedClient();

        // The field is optional: an older client that never sends it must still upload, with no
        // flags recorded rather than an error.
        var response = await client.PostAsync(
            "/api/v1/media",
            UploadWithFlags(TestImages.RealPng(20, 20), "image/png", null));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<MediaStatusResponse>();

        using var scope = factory.Services.CreateScope();
        var media = await scope.ServiceProvider.GetRequiredService<IMediaRepository>()
            .GetByIdAsync(Ulid.Parse(body!.Id));
        Assert.NotNull(media);
        Assert.False(media.SafetyFlags.IsSet("flashing"));
        Assert.Equal(MediaSafetySource.None, media.SafetySource);
    }

    [Fact]
    public async Task Upload_RequiresAuthentication()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsync("/api/v1/media", Upload(PngBytes, "image/png"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Status_OfAnotherUsersMedia_ReturnsNotFound()
    {
        var uploader = await AuthedClient();
        var upload = await uploader.PostAsync("/api/v1/media", Upload(PngBytes, "image/png"));
        var body = await upload.Content.ReadFromJsonAsync<MediaStatusResponse>();

        var otherUser = await AuthedClient();
        var response = await otherUser.GetAsync($"/api/v1/media/{body!.Id}/status");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Upload_RecordsTheContentHash_OfTheRealBytesOnTheWire()
    {
        var client = await AuthedClient();
        var pngBytes = TestImages.RealPng(24, 18);

        var response = await client.PostAsync("/api/v1/media", Upload(pngBytes, "image/png"));
        var uploaded = await response.Content.ReadFromJsonAsync<MediaStatusResponse>();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var media = await db.MediaItems.FirstAsync(m => m.Id == Ulid.Parse(uploaded!.Id));

        // The hash is of what actually arrived, computed server-side. A hash taken from the client
        // would be forgeable, so it is not exposed on the response at all — this reads the column.
        var expected = Convert.ToHexString(SHA256.HashData(pngBytes)).ToLowerInvariant();
        Assert.Equal(expected, media.ContentHash);
    }

    [Fact]
    public async Task TwoUploadsOfTheSameFile_ShareAHash()
    {
        var client = await AuthedClient();
        var pngBytes = TestImages.RealPng(24, 18);

        var first = await client.PostAsync("/api/v1/media", Upload(pngBytes, "image/png"));
        var second = await client.PostAsync("/api/v1/media", Upload(pngBytes, "image/png"));
        var firstBody = await first.Content.ReadFromJsonAsync<MediaStatusResponse>();
        var secondBody = await second.Content.ReadFromJsonAsync<MediaStatusResponse>();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hashes = await db.MediaItems
            .Where(m => m.Id == Ulid.Parse(firstBody!.Id) || m.Id == Ulid.Parse(secondBody!.Id))
            .Select(m => m.ContentHash)
            .ToListAsync();

        // Distinct rows — dedup is deliberately NOT built, this column is only its prerequisite —
        // but the two are recognisably the same file, which is the property a consumer will need.
        Assert.Equal(2, hashes.Count);
        Assert.Single(hashes.Distinct());
    }
}
