using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FriendCore.API.Realtime;

/// <summary>
///     §11.1 notification delivery. Push-only for the MVP: the server invokes "NotificationReceived"
///     on connected clients via IHubContext; clients call nothing back. [Authorize] means only
///     authenticated connections are accepted, and IUserIdProvider maps each connection to its
///     user so Clients.User(id) reaches every device that user has open.
/// </summary>
[Authorize]
public sealed class NotificationHub : Hub
{
    public const string ReceiveMethod = "NotificationReceived";
}
