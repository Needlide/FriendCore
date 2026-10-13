using System.Security.Cryptography;
using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Common;
using FriendCore.Domain.Media;
using MediatR;

namespace FriendCore.Application.Media.Commands.UploadMedia;

internal sealed class UploadMediaCommandHandler(
    ICurrentUserService currentUserService,
    IMediaRepository mediaRepository,
    IStorageService storageService,
    IMediaProcessingQueue processingQueue,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<UploadMediaCommand, ErrorOr<MediaStatusResponse>>
{
    public async Task<ErrorOr<MediaStatusResponse>> Handle(
        UploadMediaCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return CommonErrors.Unauthorized;

        var header = new byte[ImageContentTypes.HeaderLength];
        var headerLength = await request.Content.ReadAtLeastAsync(
            header, ImageContentTypes.HeaderLength, throwOnEndOfStream: false, cancellationToken);

        if (!ImageContentTypes.HeaderMatches(request.ContentType, header.AsSpan(0, headerLength)))
            return MediaErrors.HeaderMismatch;

        // Hash before storing, over the bytes exactly as received — never over a variant, and
        // never taken from the client. A third pass over a ≤20MB file costs a few tens of
        // milliseconds against an upload that already took seconds, and it has to happen before
        // the record is built because the hash is part of it.
        //
        // Rewound FIRST: the header read above left the position past the magic bytes, and
        // hashing from there would silently fingerprint every file minus its first 16 bytes —
        // which is stable enough to look correct and useless for every purpose this column serves.
        request.Content.Seek(0, SeekOrigin.Begin);
        var contentHash = await ComputeContentHashAsync(request.Content, cancellationToken);
        request.Content.Seek(0, SeekOrigin.Begin);

        var contentType = request.ContentType.ToLowerInvariant();
        var media = MediaItem.Create(
            userId,
            request.FileName,
            contentType,
            ImageContentTypes.Extension(contentType),
            request.HasFlashingWarning,
            request.HasIntenseMotionWarning,
            dateTimeProvider.UtcNow,
            contentHash);

        // Storage first (spec §7.1 steps 3–4): a failed upload leaves no dangling record, while a
        // failed save leaves only an orphaned object for the cleanup sweeper.
        await storageService.UploadAsync(media.OriginalStorageKey, request.Content, contentType, cancellationToken);
        await mediaRepository.AddAsync(media, cancellationToken);

        // If the enqueue is lost (e.g. process crash before the consumer drains it), the sweeper
        // re-enqueues anything left in Processing — the item is durable from this point on.
        //
        // Awaited, not fire-and-forget: the channel is bounded, so a full queue applies real
        // backpressure and this call waits for room rather than dropping the item. The request's
        // cancellation token is passed through so a client that disconnects mid-wait releases the
        // writer instead of holding it until capacity appears.
        await processingQueue.EnqueueAsync(media.Id, cancellationToken);

        return MediaStatusResponse.Create(media);
    }

    /// <summary>
    ///     Lowercase hex SHA-256 of the stream. The stream is rewound by the caller rather than
    ///     restored here, so the hashing pass cannot leave a half-read buffer for the storage
    ///     upload to send.
    /// </summary>
    private static async Task<string> ComputeContentHashAsync(Stream content, CancellationToken cancellationToken)
    {
        var hash = await SHA256.HashDataAsync(content, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
