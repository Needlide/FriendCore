namespace FriendCore.API.Contracts.Messaging;

/// <param name="Content">The replacement text. Required — editing to empty is a delete, not an edit.</param>
public sealed record EditMessageRequest(string Content);
