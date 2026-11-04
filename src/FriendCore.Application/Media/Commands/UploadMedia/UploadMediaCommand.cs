using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Media.Commands.UploadMedia;

/// <summary>
///     <paramref name="Content" /> must be seekable (buffered): the handler reads the header for
///     magic-byte verification, then rewinds and streams the whole file to object storage.
/// </summary>
/// <remarks>
///     <paramref name="SafetyFlags" /> is one dictionary ("flashing" → true, ...) instead of
///     one bool per hazard, so adding a new flag never changes this command.
/// </remarks>
public sealed record UploadMediaCommand(
    Stream Content,
    long Length,
    string FileName,
    string ContentType,
    IReadOnlyDictionary<string, bool> SafetyFlags) : IRequest<ErrorOr<MediaStatusResponse>>;
