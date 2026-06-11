using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Options;
using FriendCore.Domain.Media;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace FriendCore.Infrastructure.MediaProcessing;

/// <summary>
///     Spec §7.1 steps 7–11: download the original, generate WebP variants, mark the item Ready.
///     Idempotent end to end — a duplicate run regenerates the same keys and MarkReady no-ops on
///     an already-finalised item — so sweeper re-enqueues are always safe.
/// </summary>
internal sealed class ProcessMediaJob(
    IMediaRepository mediaRepository,
    IStorageService storageService,
    IOptions<MediaProcessingOptions> options,
    ILogger<ProcessMediaJob> logger)
{
    public async Task ProcessAsync(Ulid mediaId, CancellationToken cancellationToken)
    {
        var media = await mediaRepository.GetByIdAsync(mediaId, cancellationToken);
        if (media is null || media.Status != MediaStatus.Processing)
            return;

        try
        {
            var variants = await GenerateVariantsAsync(media, cancellationToken);
            media.MarkReady(variants);
        }
        catch (OperationCanceledException)
        {
            // Host shutdown mid-job: leave the item in Processing for the sweeper after restart.
            throw;
        }
        catch (Exception exception)
        {
            // Undecodable/corrupt image or storage failure: the client sees Failed and re-uploads.
            logger.LogError(exception, "Media processing failed for {MediaId}", mediaId);
            media.MarkFailed();
        }

        await mediaRepository.UpdateAsync(cancellationToken);
    }

    private async Task<List<MediaVariant>> GenerateVariantsAsync(MediaItem media, CancellationToken cancellationToken)
    {
        // Buffer locally: the original is read once but processed into four variants.
        using var original = new MemoryStream();
        await using (var download = await storageService.DownloadAsync(media.OriginalStorageKey, cancellationToken))
        {
            await download.CopyToAsync(original, cancellationToken);
        }

        original.Position = 0;
        using var image = await Image.LoadAsync(original, cancellationToken);

        // Bake the EXIF orientation into the pixels first, then strip metadata — uploads carry
        // GPS coordinates and device info the platform must not republish. The ICC colour profile
        // stays: it affects rendering, not privacy.
        image.Mutate(context => context.AutoOrient());
        image.Metadata.ExifProfile = null;
        image.Metadata.XmpProfile = null;
        image.Metadata.IptcProfile = null;

        var variants = new List<MediaVariant>
        {
            // Metadata row for the preserved original (dimensions/size for clients); points at the
            // original's own key and is only ever served via presigned URL.
            MediaVariant.Create(
                MediaVariantType.Original, media.OriginalStorageKey, image.Width, image.Height, original.Length)
        };

        var encoder = new WebpEncoder { Quality = options.Value.WebpQuality };

        foreach (var (type, size, mode) in MediaVariantPlanner.Plan(image.Width, image.Height))
        {
            using var variantImage = image.Clone(context =>
                context.Resize(new ResizeOptions { Size = size, Mode = mode }));

            using var output = new MemoryStream();
            await variantImage.SaveAsync(output, encoder, cancellationToken);
            output.Position = 0;

            var key = MediaStorageKeys.Variant(media.Id, type);
            await storageService.UploadAsync(key, output, "image/webp", cancellationToken);

            variants.Add(MediaVariant.Create(type, key, variantImage.Width, variantImage.Height, output.Length));
        }

        return variants;
    }
}
