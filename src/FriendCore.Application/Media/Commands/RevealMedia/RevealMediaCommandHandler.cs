using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Application.Common.Options;
using FriendCore.Domain.Common;
using FriendCore.Domain.Media;
using FriendCore.Domain.Messaging;
using FriendCore.Domain.Posts;
using FriendCore.Domain.Users;
using Microsoft.Extensions.Options;
using MediatR;

namespace FriendCore.Application.Media.Commands.RevealMedia;

/// <summary>
/// The consent gate for flagged media (§7.4).
///
/// Flagged media ships with no variant URLs in any list response, so this is the only path to
/// the bytes. Authorization is not "is this user authenticated" — it is "would this user have
/// been able to see this image in the first place", which depends entirely on what the media
/// is attached to:
///   - a post    → visible to whoever can see the post (block-filtered lookup does the work);
///   - a message → only to that conversation's participants, with the §8.2 block intersection;
///   - unattached → only the uploader, so a draft can be previewed.
/// Every failure is a 404, never a 403: telling a stranger that a media id exists in a
/// conversation they are not in is itself a leak.
/// </summary>
internal sealed class RevealMediaCommandHandler(
    ICurrentUserService currentUserService,
    IMediaRepository mediaRepository,
    IPostRepository postRepository,
    IMessagingRepository messagingRepository,
    IBlockRepository blockRepository,
    IOptions<StorageOptions> storageOptions)
    : IRequestHandler<RevealMediaCommand, ErrorOr<MediaVariantsResponse>>
{
    private readonly string? _publicBaseUrl = storageOptions.Value.PublicBaseUrl?.TrimEnd('/');

    public async Task<ErrorOr<MediaVariantsResponse>> Handle(
        RevealMediaCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return CommonErrors.Unauthorized;

        var media = await mediaRepository.GetByIdAsync(request.MediaId, cancellationToken);
        if (media is null)
            return MediaErrors.NotFound;

        if (!await MaySeeAsync(media, userId, cancellationToken))
            return MediaErrors.NotFound;

        return new MediaVariantsResponse(
            media.Id.ToString(),
            MediaResponse.Create(media, _publicBaseUrl, includeVariants: true).Variants);
    }

    private async Task<bool> MaySeeAsync(MediaItem media, Ulid userId, CancellationToken cancellationToken)
    {
        // An unflagged item was never withheld, so there is nothing to authorize — refusing it
        // here would break the ordinary path for no privacy gain.
        if (!media.HasFlashingWarning && !media.HasIntenseMotionWarning)
            return true;

        // Your own upload: needed so a draft's flagged image is still previewable, and the
        // declaration came from you in the first place.
        if (media.UploaderId == userId)
            return true;

        var attachment = await mediaRepository.GetAttachmentAsync(media.Id, cancellationToken);

        if (attachment.PostId is { } postId)
        {
            // Block-filtered: a post from someone you blocked reads as absent, which is exactly
            // the §8.2 behaviour every other post surface already has.
            var post = await postRepository.GetByIdAsync(postId, cancellationToken);
            return post is not null;
        }

        if (attachment.MessageId is { } messageId && attachment.ConversationId is { } conversationId)
        {
            var conversation = await messagingRepository.GetByIdAsync(conversationId, cancellationToken);
            if (conversation is null || !conversation.HasParticipant(userId))
                return false;

            // §8.2: a block ends access to the conversation, and therefore to the image in it.
            if (conversation.OtherParticipantId(userId) is { } otherId
                && await blockRepository.ExistsBetweenAsync(userId, otherId, cancellationToken))
                return false;

            return true;
        }

        // Flagged, and attached to nothing but owned by someone else — not disclosable.
        return false;
    }
}
