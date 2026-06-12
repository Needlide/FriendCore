using FriendCore.Application.Posts.Commands.CreatePost;

namespace FriendCore.API.Contracts.Posts;

public sealed record CreatePostRequest(
    string? Content,
    PostSensitivity Sensitivity,
    IReadOnlyList<string>? MediaIds = null,
    IReadOnlyList<string>? Tags = null);
