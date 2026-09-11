using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Users.Queries.GetPreferences;

public sealed record GetPreferencesQuery : IRequest<ErrorOr<PreferencesResponse>>;
