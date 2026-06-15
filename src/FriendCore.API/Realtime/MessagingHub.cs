using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FriendCore.API.Realtime;

/// <summary>
///     §11.1 real-time DM delivery. Push-only: sending happens over REST (POST /messages); this
///     hub only delivers. The server invokes "MessageReceived" on the recipient's connections.
/// </summary>
[Authorize]
public sealed class MessagingHub : Hub
{
    public const string ReceiveMethod = "MessageReceived";
}
