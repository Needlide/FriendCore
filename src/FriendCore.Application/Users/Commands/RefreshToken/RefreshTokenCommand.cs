using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Users.Commands.RefreshToken;

/// <summary>Exchanges a valid refresh token (from the HttpOnly cookie) for a fresh access token,
/// rotating the refresh token. The raw value comes from the API, not the request body.</summary>
public sealed record RefreshTokenCommand(string RefreshToken) : IRequest<ErrorOr<AuthTokens>>;
