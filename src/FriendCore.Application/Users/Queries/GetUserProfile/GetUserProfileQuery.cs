using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Users.Queries.GetUserProfile;

public sealed record GetUserProfileQuery(string Username) : IRequest<ErrorOr<UserProfileResponse>>;