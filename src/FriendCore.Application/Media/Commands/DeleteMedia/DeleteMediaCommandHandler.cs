using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Domain.Common;
using FriendCore.Domain.Media;
using FriendCore.Domain.Posts;
using MediatR;

namespace FriendCore.Application.Media.Commands.DeleteMedia;

internal sealed class DeleteMediaCommandHandler(
    ICurrentUserService currentUserService,
    IMediaRepository mediaRepository,
    IPostRepository postRepository)
    : IRequestHandler<DeleteMediaCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(DeleteMediaCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return CommonErrors.Unauthorized;

        var media = await mediaRepository.GetByIdAsync(request.MediaId, cancellationToken);

        // NotFound (not Forbidden) for someone else's media — don't reveal that the id exists.
        if (media is null || media.UploaderId != userId)
            return MediaErrors.NotFound;

        // Deleting media out from under a post or a message would leave an attachment-only
        // message with nothing in it; the owning record is the unit of deletion once attached.
        var attached = await mediaRepository.GetAttachedIdsAsync([media.Id], cancellationToken);
        if (attached.Count > 0)
            return MediaErrors.AttachedToPost;

        // Soft delete only — the stored objects stay, consistent with the soft-delete posture
        // everywhere else; physical storage cleanup is a future sweep.
        await mediaRepository.SoftDeleteAsync(media, cancellationToken);
        return Result.Success;
    }
}
