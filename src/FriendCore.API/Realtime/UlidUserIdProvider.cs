using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace FriendCore.API.Realtime;

/// <summary>
///     Tells SignalR which user a connection belongs to, so Clients.User(userId) works. Reads the
///     same claims as HttpCurrentUserService (NameIdentifier, falling back to the raw JWT "sub")
///     so the id format matches the Ulid strings we push to.
/// </summary>
public sealed class UlidUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        return connection.User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? connection.User.FindFirstValue(JwtRegisteredClaimNames.Sub);
    }
}
