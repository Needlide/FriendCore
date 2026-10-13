using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Messaging;

namespace FriendCore.Application.Common;

/// <summary>
///     Builds the quoted-parent reference for messages that reply to another message. One lookup
///     per page, exactly like <see cref="MediaResponseFactory" /> — a per-reply lookup would be a
///     second round trip for every reply on the screen.
/// </summary>
/// <remarks>
///     Lives in Common because the same shape is needed by three paths: the thread feed, the send
///     response, and the edit response. The last one is easy to forget — an edit pushes the whole
///     message, so a reply edited without re-resolving its parent would push a bubble whose
///     quote had silently vanished.
/// </remarks>
internal sealed class ReplyReferenceFactory(IMessagingRepository messagingRepository)
{
    /// <summary>
    ///     Resolves every reply target among <paramref name="messages" />, keyed by the reply's
    ///     own message id. Messages that reply to nothing are absent from the dictionary.
    /// </summary>
    public async Task<IReadOnlyDictionary<Ulid, MessageReferenceResponse>> ForMessagesAsync(
        IReadOnlyCollection<Message> messages,
        CancellationToken cancellationToken)
    {
        var parentIds = messages
            .Where(message => message.ReplyToMessageId is not null)
            .Select(message => message.ReplyToMessageId!.Value)
            .Distinct()
            .ToList();

        if (parentIds.Count == 0)
            return new Dictionary<Ulid, MessageReferenceResponse>();

        var parents = await messagingRepository.GetByIdsIncludingDeletedAsync(parentIds, cancellationToken);

        return messages
            .Where(message => message.ReplyToMessageId is not null
                             && parents.ContainsKey(message.ReplyToMessageId.Value))
            // A target with no row at all is unreachable in practice: the send path refuses one
            // that is missing or deleted, so every stored target existed once. If a row genuinely
            // cannot be loaded the quote is omitted rather than fabricated — a missing quote is
            // better than one claiming to be something it is not.
            .ToDictionary(
                message => message.Id,
                message => ToResponse(MessageReference.From(parents[message.ReplyToMessageId!.Value])));
    }

    /// <summary>
    ///     The reference for a single message's reply target, or null when it replies to nothing.
    /// </summary>
    public async Task<MessageReferenceResponse?> ForMessageAsync(
        Message message,
        CancellationToken cancellationToken)
    {
        if (message.ReplyToMessageId is not { } parentId)
            return null;

        var parents = await messagingRepository.GetByIdsIncludingDeletedAsync([parentId], cancellationToken);

        return parents.TryGetValue(parentId, out var parent)
            ? ToResponse(MessageReference.From(parent))
            : null;
    }

    /// <summary>Domain record → wire form. Ids are strings here, as everywhere in this family.</summary>
    private static MessageReferenceResponse ToResponse(MessageReference reference) =>
        new(
            reference.Id.ToString(),
            reference.SenderId.ToString(),
            reference.Excerpt,
            reference.WasDeleted);
}