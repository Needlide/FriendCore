using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Posts.Commands.CreatePost;

/// <summary>
///     MediaIds are Ulid strings in display order (validated parseable by the validator); the
///     referenced media must belong to the author and be Ready. Tags are display names — the
///     handler runs them through the §6.2 normalisation pipeline and creates missing tags
///     (PendingReview-but-usable, same as profile tags).
/// </summary>
public sealed record CreatePostCommand(
    string? Content,
    PostSensitivity Sensitivity,
    IReadOnlyList<string> MediaIds,
    IReadOnlyList<string> Tags)
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
