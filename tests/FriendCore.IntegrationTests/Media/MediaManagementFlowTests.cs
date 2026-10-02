using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Media;
using FriendCore.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace FriendCore.IntegrationTests.Media;

/// <summary>Spec §7 management endpoints: media deletion and safety reporting (§7.4 layer 2).</summary>
public class MediaManagementFlowTests(FriendCoreApiFactory factory) : IClassFixture<FriendCoreApiFactory>
{
    private async Task<HttpClient> AuthedClient() =>
        (await new AuthScenarios(factory).RegisterVerifiedClientAsync()).Client;

    private static async Task<string> UploadReadyMedia(HttpClient client)
    {
        var file = new ByteArrayContent(TestImages.RealPng(60, 40));
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        var form = new MultipartFormDataContent { { file, "file", "image.png" } };

        var response = await client.PostAsync("/api/v1/media", form);
        var body = await response.Content.ReadFromJsonAsync<MediaStatusResponse>();
        Assert.Equal("Ready", await MediaUploadFlowTests.PollStatusAsync(client, body!.Id));
        return body.Id;
    }

    [Fact]
    public async Task Delete_OwnUnattachedMedia_Succeeds_AndHidesIt()
    {
        var client = await AuthedClient();
        var mediaId = await UploadReadyMedia(client);

        var response = await client.DeleteAsync($"/api/v1/media/{mediaId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var status = await client.GetAsync($"/api/v1/media/{mediaId}/status");
        Assert.Equal(HttpStatusCode.NotFound, status.StatusCode);
    }

    [Fact]
    public async Task Delete_MediaAttachedToPost_ReturnsConflict()
    {
        var client = await AuthedClient();
        var mediaId = await UploadReadyMedia(client);
        var post = await client.PostAsJsonAsync(
            "/api/v1/posts", new { content = "with media", sensitivity = "General", mediaIds = new[] { mediaId } });
        Assert.Equal(HttpStatusCode.Created, post.StatusCode);

        var response = await client.DeleteAsync($"/api/v1/media/{mediaId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("Media.AttachedToPost", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Delete_AnotherUsersMedia_ReturnsNotFound()
    {
        var owner = await AuthedClient();
        var mediaId = await UploadReadyMedia(owner);

        var other = await AuthedClient();
        var response = await other.DeleteAsync($"/api/v1/media/{mediaId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Report_ByAnyUser_GatesTheMedia_AndReachesTheModeratorQueue()
    {
        var owner = await AuthedClient();
        var mediaId = await UploadReadyMedia(owner);

        var reporter = await AuthedClient();
        var response = await reporter.PostAsJsonAsync(
            $"/api/v1/media/{mediaId}/report", new { category = "FlashingLights", note = "strobes badly" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var media = await scope.ServiceProvider.GetRequiredService<IMediaRepository>()
            .GetByIdAsync(Ulid.Parse(mediaId));
        Assert.True(media!.HasFlashingWarning);
        Assert.True(media.HasIntenseMotionWarning);
        Assert.Equal(MediaSafetySource.UserReports, media.SafetySource);
    }

    [Fact]
    public async Task Report_Self_ReturnsBadRequest()
    {
        var owner = await AuthedClient();
        var mediaId = await UploadReadyMedia(owner);

        var response = await owner.PostAsJsonAsync(
            $"/api/v1/media/{mediaId}/report", new { category = "FlashingLights" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Report_InvalidCategory_ReturnsBadRequest()
    {
        var client = await AuthedClient();
        var mediaId = await UploadReadyMedia(client);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/media/{mediaId}/report", new { category = "NotACategory" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Report_UnknownMedia_ReturnsNotFound()
    {
        var client = await AuthedClient();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/media/{Ulid.NewUlid()}/report", new { category = "FlashingLights" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
