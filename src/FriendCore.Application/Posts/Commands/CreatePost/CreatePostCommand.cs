using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Posts.Commands.CreatePost;

/// <summary>
///     MediaIds are Ulid strings in display order (validated parseable by the validator); the
///     referenced media must belong to the author and be Ready.
/// </summary>
public sealed record CreatePostCommand(
    string? Content,
    PostSensitivity Sensitivity,
    IReadOnlyList<string> MediaIds)
    : IRequest<ErrorOr<PostResponse>>;

// Application-level mirror of Domain ContentSensitivity so API contracts never bind Domain
// types (§2.2 layering). Member names are the wire format (JsonStringEnumConverter) — renaming
// here is a breaking API change; renaming the Domain enum is not.
public enum PostSensitivity
{
    General,
    Sensitive,
    Explicit
}
