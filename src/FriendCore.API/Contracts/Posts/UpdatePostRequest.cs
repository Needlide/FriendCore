using FriendCore.Application.Posts.Commands.CreatePost;

namespace FriendCore.API.Contracts.Posts;

public sealed record UpdatePostRequest(string? Content, PostSensitivity Sensitivity);
