using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Common;
using FriendCore.Domain.Media;
using MediatR;

namespace FriendCore.Application.Media.Queries.GetMediaStatus;

internal sealed class GetMediaStatusQueryHandler(
    ICurrentUserService currentUserService,
    IMediaRepository mediaRepository)
    : IRequestHandler<GetMediaStatusQuery, ErrorOr<MediaStatusResponse>>
{
    public async Task<ErrorOr<MediaStatusResponse>> Handle(
        GetMediaStatusQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return CommonErrors.Unauthorized;

        var media = await mediaRepository.GetByIdAsync(request.MediaId, cancellationToken);

        // NotFound (not Forbidden) for someone else's media — don't reveal that the id exists.
        if (media is null || media.UploaderId != userId)
            return MediaErrors.NotFound;

        return MediaStatusResponse.Create(media);
    }
}
