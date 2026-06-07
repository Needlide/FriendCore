using FriendCore.Domain.Posts;

namespace FriendCore.API.Contracts.Posts;

public sealed record CreatePostRequest(string? Content, ContentSensitivity Sensitivity);
