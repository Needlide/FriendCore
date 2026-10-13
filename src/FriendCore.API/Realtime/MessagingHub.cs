using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FriendCore.API.Realtime;

/// <summary>
///     §11.1 real-time DM delivery. Push-only: sending happens over REST (POST /messages); this
///     hub only delivers. The server invokes "MessageReceived" on the recipient's connections,
///     and the "MessageEdited"/"MessageDeleted" reconciliations on both participants'.
/// </summary>
[Authorize]
public sealed class MessagingHub : Hub
{
    public const string ReceiveMethod = "MessageReceived";

    // Beyond "a message arrived": the client already holds the message in both cases, so these
    // carry the id and the full current state rather than telling it to refetch blindly.
    public const string EditedMethod = "MessageEdited";
    public const string DeletedMethod = "MessageDeleted";

    // Same rationale as the edit: the tapper has the POST response, but the other participant's
    // open thread has to move the chip, and the tapper's other devices would go stale.
    public const string ReactionChangedMethod = "MessageReactionChanged";
}
