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

        request.Content.Seek(0, SeekOrigin.Begin);

        var contentType = request.ContentType.ToLowerInvariant();
        var media = MediaItem.Create(
            userId,
            request.FileName,
            contentType,
            ImageContentTypes.Extension(contentType),
            request.HasFlashingWarning,
            request.HasIntenseMotionWarning,
            dateTimeProvider.UtcNow);

        // Storage first (spec §7.1 steps 3–4): a failed upload leaves no dangling record, while a
        // failed save leaves only an orphaned object for the cleanup sweeper.
        await storageService.UploadAsync(media.OriginalStorageKey, request.Content, contentType, cancellationToken);
        await mediaRepository.AddAsync(media, cancellationToken);

        // If the enqueue is lost (e.g. process crash before the consumer drains it), the sweeper
        // re-enqueues anything left in Processing — the item is durable from this point on.
        processingQueue.Enqueue(media.Id);

        return MediaStatusResponse.Create(media);
    }
}
