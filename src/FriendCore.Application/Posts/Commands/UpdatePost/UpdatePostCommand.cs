using ErrorOr;
using FriendCore.Application.Common.Models;
using FriendCore.Application.Posts.Commands.CreatePost;
using MediatR;

namespace FriendCore.Application.Posts.Commands.UpdatePost;

/// <summary>
///     §5.3 MVP: only text and sensitivity are editable. Media and tags stay as created, so a
///     post that is image-only may have its caption cleared but never its media.
/// </summary>
public sealed record UpdatePostCommand(Ulid PostId, string? Content, PostSensitivity Sensitivity)
    : IRequest<ErrorOr<PostResponse>>;
