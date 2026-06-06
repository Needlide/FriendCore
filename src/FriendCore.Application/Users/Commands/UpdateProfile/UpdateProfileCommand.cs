using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Users.Commands.UpdateProfile;

public sealed record UpdateProfileCommand(
    string? DisplayName,
    string? Bio,
    string? WebsiteUrl,
    string? Location) : IRequest<ErrorOr<UserProfileResponse>>;
