using ErrorOr;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Posts;
using MediatR;

namespace FriendCore.Application.Posts.Commands.CreatePost;

public sealed record CreatePostCommand(string? Content, ContentSensitivity Sensitivity)
    : IRequest<ErrorOr<PostResponse>>;
