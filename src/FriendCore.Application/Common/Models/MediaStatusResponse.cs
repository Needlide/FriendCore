using FriendCore.Domain.Media;

namespace FriendCore.Application.Common.Models;

/// <summary>
///     Returned by the upload endpoint (spec §7.1 step 6: imageId + processing status) and by the
///     status-polling endpoint clients use until the media is Ready.
/// </summary>
public sealed record MediaStatusResponse(string Id, string Status)
{
    public static MediaStatusResponse Create(MediaItem media)
    {
        return new MediaStatusResponse(media.Id.ToString(), media.Status.ToString());
    }
}
