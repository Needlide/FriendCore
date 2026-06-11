using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Media.Commands.UploadMedia;

/// <summary>
///     <paramref name="Content" /> must be seekable (buffered): the handler reads the header for
///     magic-byte verification, then rewinds and streams the whole file to object storage.
/// </summary>
public sealed record UploadMediaCommand(
    Stream Content,
    long Length,
    string FileName,
    string ContentType,
    bool HasFlashingWarning,
    bool HasIntenseMotionWarning) : IRequest<ErrorOr<MediaStatusResponse>>;
