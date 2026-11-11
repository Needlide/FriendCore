using FriendCore.Domain.Media;
using FriendCore.Domain.Messaging;
using Microsoft.EntityFrameworkCore;

namespace FriendCore.Infrastructure.Persistence.Repositories;

public sealed class MessagingRepository(AppDbContext context) : IMessagingRepository
{
    // Tracked (no AsNoTracking): callers mutate the loaded aggregate (RecordMessage / MarkRead)
    // and rely on SaveChanges to flush. Owned participants are auto-included.
    public Task<Conversation?> GetByIdAsync(Ulid conversationId, CancellationToken cancellationToken = default)
    {
        return context.Conversations.FirstOrDefaultAsync(
            conversation => conversation.Id == conversationId, cancellationToken);
    }

    public Task<Conversation?> FindDirectAsync(Ulid userA, Ulid userB, CancellationToken cancellationToken = default)
    {
        // All conversations are 1:1 in the MVP, so "both are participants" uniquely identifies one.
        return context.Conversations.FirstOrDefaultAsync(
            conversation => conversation.Participants.Any(p => p.UserId == userA)
                            && conversation.Participants.Any(p => p.UserId == userB),
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Conversation> AddConversationOrGetExistingAsync(
        Conversation conversation,
        CancellationToken cancellationToken = default)
    {
        await context.Conversations.AddAsync(conversation, cancellationToken);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return conversation;
        }
        catch (DbUpdateException)
        {
            // Lost the race, which is a success and not a failure: the other request created the
            // very conversation we were trying to make. Only the direct-key conflict is
            // recoverable — swallowing any other DbUpdateException would turn a real fault into a
            // silently wrong result that looks like success.
            var directKey = Conversation.DirectKeyFor(
                conversation.Participants[0].UserId, conversation.Participants[1].UserId);

            var conflict = context.Database
                .SqlQueryRaw<int>(
                    "SELECT 1 FROM conversations WHERE direct_key = {0} LIMIT 1", directKey)
                .AsEnumerable()
                .Any();

            if (!conflict)
                throw;

            // The failed insert left the entity in Added state; detaching stops SaveChanges from
            // retrying it on every subsequent call in this request.
            context.Entry(conversation).State = EntityState.Detached;

            var existing = await FindDirectAsync(
                conversation.Participants[0].UserId,
                conversation.Participants[1].UserId,
                cancellationToken);

            if (existing is null)
                throw;

            return existing;
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return context.SaveChangesAsync(cancellationToken);
    }

    public async Task AddMessageAsync(Message message, CancellationToken cancellationToken = default)
    {
        // One SaveChanges flushes the new message together with the tracked conversation's
        // LastMessageAt bump.
        await context.Messages.AddAsync(message, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    // Tracked (no AsNoTracking): Edit() and MarkDeleted() mutate the loaded entity and
    // SoftDeleteAsync/SaveChangesAsync flush it.
    public Task<Message?> GetMessageAsync(
        Ulid conversationId,
        Ulid messageId,
        CancellationToken cancellationToken = default)
    {
        return context.Messages
            // The edit response and the refusal path both read the images, and a loaded message
            // without them would look like a text-only bubble after an edit.
            .Include(message => message.Media)
            .FirstOrDefaultAsync(
                message => message.Id == messageId && message.ConversationId == conversationId,
                cancellationToken);
    }

    public async Task SoftDeleteAsync(
        Message message,
        DateTimeOffset deletedAt,
        MessageDeletionSource source,
        CancellationToken cancellationToken = default)
    {
        await StageSoftDeleteCoreAsync(message, deletedAt, source, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task<Message?> GetMessageForModerationAsync(Ulid messageId, CancellationToken cancellationToken = default)
    {
        return context.Messages
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(message => message.Id == messageId, cancellationToken);
    }

    /// <summary>
    ///     Stages a soft delete for the moderator's own SaveChanges, so the removal and the audit
    ///     entry commit together (§10.3). Awaits a read (the media hash) but never saves.
    /// </summary>
    public async Task StageSoftDeleteAsync(
        Message message,
        DateTimeOffset deletedAt,
        MessageDeletionSource source,
        CancellationToken cancellationToken = default)
    {
        await StageSoftDeleteCoreAsync(message, deletedAt, source, cancellationToken);
    }

    /// <summary>
    ///     Everything a message deletion does apart from committing it, so the saving and staging
    ///     paths cannot drift apart.
    /// </summary>
    /// <remarks>
    ///     Async only because it looks up the image's content hash for a possible consent
    ///     refusal. Nothing here writes to the database — that is the caller's SaveChanges.
    /// </remarks>
    private async Task StageSoftDeleteCoreAsync(
        Message message,
        DateTimeOffset deletedAt,
        MessageDeletionSource source,
        CancellationToken cancellationToken)
    {
        // Deliberately not context.Messages.Remove(message): the interceptor would stamp
        // DeletedAt and nothing else, leaving the text readable in the row. MarkDeleted is what
        // scrubs it. The row itself is never hard-deleted here — §2.3 soft deletes.
        message.MarkDeleted(deletedAt);

        // Reactions go with it: there is no FK from message_reactions to messages (the same
        // deliberate choice the rest of the messaging tables make), so nothing else would ever
        // clean these up and they would sit on the row as unreachable orphans.
        await RemoveReactionsForMessageAsync(message.Id, cancellationToken);

        // One refusal per image: deleting a four-image message must not stop the other three from
        // coming back with a re-upload.
        context.MediaRefusals.AddRange(
            await BuildRefusalsAsync(message, source, deletedAt, cancellationToken));
    }

    /// <summary>
    ///     The consent refusal this deletion implies, or null when it implies none.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         A sender withdrawing their own message implies none. They declined to send it,
    ///         which is nobody else's wish being overridden — and recording it would permanently
    ///         stop them re-sharing that exact image with that person later, which is confusing
    ///         friction for no protective gain. The abuse case this exists for, B deleting and A
    ///         resending, is covered by B's deletion on the first pass regardless.
    ///     </para>
    ///     <para>
    ///         A recipient's deletion and a moderator's removal both mean the image should not
    ///         come back between these two people, so both record. The judgement lives in
    ///         <see cref="MessageDeletionSource" /> rather than being re-derived at each call site.
    ///     </para>
    /// </remarks>
    private async Task<IReadOnlyList<MediaRefusal>> BuildRefusalsAsync(
        Message message,
        MessageDeletionSource source,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        if (source == MessageDeletionSource.Sender)
            return [];

        // Read the junction rather than the navigation: hide/delete callers load the message
        // without Include, and a refusal derived from an empty collection would let a deleted
        // image come straight back.
        var mediaItemIds = await context.MessageMedia
            .Where(link => link.MessageId == message.Id)
            .Select(link => link.MediaItemId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (mediaItemIds.Count == 0)
            return [];

        // The recipient is whoever this conversation is with. Resolved from the conversation
        // rather than passed in, so it cannot disagree with the message it is deleting.
        var conversation = await context.Conversations
            .Include(candidate => candidate.Participants)
            .FirstOrDefaultAsync(
                candidate => candidate.Id == message.ConversationId,
                cancellationToken);

        var recipientId = conversation?.OtherParticipantId(message.SenderId);
        if (recipientId is null)
            return [];

        // A media item that predates the ContentHash column has no hash, and refusing an empty
        // string would be worse than not refusing: it would match nothing, but it would also mean
        // this code had silently started pretending to check.
        var hashes = await context.MediaItems
            .Where(media => mediaItemIds.Contains(media.Id))
            .Select(media => media.ContentHash)
            .ToListAsync(cancellationToken);

        return hashes
            .Where(hash => !string.IsNullOrEmpty(hash))
            .Select(hash => MediaRefusal.Create(hash!, recipientId.Value, message.SenderId, at))
            .ToList();
    }

    /// <summary>
    ///     Records a participant hiding a message from their own view, plus the consent refusal
    ///     the hiding implies.
    /// </summary>
    /// <remarks>
    ///     Nothing about the message changes. Deliberately does not call MarkDeleted, does not
    ///     remove reactions, and does not un-attach the image — the sender keeps all of it, and
    ///     the report queue can still resolve against this row afterwards.
    /// </remarks>
    public async Task HideMessageAsync(
        Ulid messageId,
        Ulid userId,
        DateTimeOffset at,
        MessageDeletionSource source,
        CancellationToken cancellationToken = default)
    {
        // Hiding twice is a no-op rather than a duplicate row; the (message, user) key would
        // otherwise surface as a constraint violation the user has no way to cause or recover from.
        var alreadyHidden = await context.MessageHidden
            .AnyAsync(hidden => hidden.MessageId == messageId && hidden.UserId == userId, cancellationToken);

        if (alreadyHidden)
            return;

        context.MessageHidden.Add(MessageHidden.Create(messageId, userId, at));

        // The refusal is the whole protective point of hiding, so it is not optional here: it is
        // what stops the image coming back, since the sender is never told it was hidden.
        var message = await context.Messages.FirstOrDefaultAsync(
            candidate => candidate.Id == messageId,
            cancellationToken);

        if (message is not null)
        {
            context.MediaRefusals.AddRange(
                await BuildRefusalsAsync(message, source, at, cancellationToken));
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Conversation>> GetConversationsForUserAsync(
        Ulid userId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        return await context.Conversations
            .Where(conversation => conversation.Participants.Any(p => p.UserId == userId))
            .OrderByDescending(conversation => conversation.LastMessageAt)
            .Take(limit)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    ///     One page of the conversation for ONE viewer — <paramref name="viewerId" /> is not
    ///     decoration: hiding is per participant, so the same conversation legitimately returns
    ///     different messages to each side.
    /// </summary>
    public async Task<IReadOnlyList<Message>> GetMessagesAsync(
        Ulid conversationId,
        Ulid viewerId,
        (DateTimeOffset CreatedAt, Ulid Id)? cursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var query = context.Messages
            .Where(message => message.ConversationId == conversationId)
            // NOT a global query filter: that would hide rows from the moderation and report paths
            // too, which still need to resolve the message they are reviewing.
            .Where(message =>
                !context.MessageHidden.Any(hidden =>
                    hidden.MessageId == message.Id && hidden.UserId == viewerId));

        if (cursor is { } before)
            query = query.Where(message =>
                message.CreatedAt < before.CreatedAt ||
                (message.CreatedAt == before.CreatedAt && message.Id.CompareTo(before.Id) < 0));

        return await query
            .Include(message => message.Media)
            .OrderByDescending(message => message.CreatedAt)
            .ThenByDescending(message => message.Id)
            .Take(limit)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Message>> SearchMessagesAsync(
        Ulid conversationId,
        Ulid viewerId,
        string term,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var needle = term.ToLowerInvariant();

        return await context.Messages
            .Where(message => message.ConversationId == conversationId
                              && message.Content != null
                              && message.Content.ToLower().Contains(needle)
                              && !context.MessageHidden.Any(hidden =>
                                  hidden.MessageId == message.Id && hidden.UserId == viewerId))
            .Include(message => message.Media)
            .OrderByDescending(message => message.CreatedAt)
            .ThenByDescending(message => message.Id)
            .Take(limit)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    ///     A two-way window centred on one message.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Two ordered queries rather than one window function: each is an indexed keyset
    ///         range, and the alternative would fetch the whole conversation to sort in memory.
    ///     </para>
    ///     <para>
    ///         Each side takes <c>radius + 1</c> and discards the extra, which is how HasOlder and
    ///         HasNewer are known without a COUNT. Counting the rows around a message is the one
    ///         thing that would turn a cheap range scan into a full pass.
    ///     </para>
    ///     <para>
    ///         The hidden filter matches the read path, so jumping cannot surface something the
    ///         reader hid — including, awkwardly, the message they just searched for.
    ///     </para>
    /// </remarks>
    public async Task<MessageWindow> GetMessagesAroundAsync(
        Ulid conversationId,
        Ulid viewerId,
        Ulid messageId,
        int radius,
        CancellationToken cancellationToken = default)
    {
        var visible = context.Messages
            .Where(message => message.ConversationId == conversationId
                              && !context.MessageHidden.Any(hidden =>
                                  hidden.MessageId == message.Id && hidden.UserId == viewerId));

        var target = await visible
            .Include(message => message.Media)
            .FirstOrDefaultAsync(message => message.Id == messageId, cancellationToken);
        if (target is null)
            return MessageWindow.Empty;

        var at = target.CreatedAt;

        // Older, nearest first then reversed so the window comes out oldest-first like every
        // other list. The target itself is excluded by the strict inequality on the timestamp.
        var older = await visible
            .Where(message => message.CreatedAt < at || (message.CreatedAt == at && message.Id != target.Id))
            .Include(message => message.Media)
            .OrderByDescending(message => message.CreatedAt)
            .ThenByDescending(message => message.Id)
            .Take(radius + 1)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var newer = await visible
            .Where(message => message.CreatedAt > at)
            .Include(message => message.Media)
            .OrderBy(message => message.CreatedAt)
            .ThenBy(message => message.Id)
            .Take(radius + 1)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var hasOlder = older.Count > radius;
        var hasNewer = newer.Count > radius;

        if (hasOlder) older.RemoveAt(older.Count - 1);
        if (hasNewer) newer.RemoveAt(newer.Count - 1);

        var window = new List<Message>(older.Count + 1 + newer.Count);
        window.AddRange(older.AsEnumerable().Reverse());
        window.Add(target);
        window.AddRange(newer);

        return new MessageWindow(window, hasOlder, hasNewer);
    }

    public Task<int> CountUnreadAsync(
        Ulid conversationId,
        Ulid userId,
        DateTimeOffset? lastReadAt,
        CancellationToken cancellationToken = default)
    {
        return context.Messages.CountAsync(
            message => message.ConversationId == conversationId
                       && message.SenderId != userId
                       && (lastReadAt == null || message.CreatedAt > lastReadAt)
                       // Hiding an unread message must take it off the badge, or the count would
                       // sit at a number the user can no longer do anything about.
                       && !context.MessageHidden.Any(hidden =>
                           hidden.MessageId == message.Id && hidden.UserId == userId),
            cancellationToken);
    }

    /// <summary>
    /// The edit's single write: one statement that checks the version, writes the text, keeps
    /// the FIRST edit time, and bumps the version. Returns false if the row had moved on.
    /// </summary>
    /// <remarks>
    /// Two things this replaces, both of which were read-modify-write and therefore racy. The
    /// version check in SQL closes the window between the handler loading the row and this write
    /// landing — a plain in-memory comparison cannot see a concurrent save. And
    /// <c>COALESCE(edited_at, ...)</c> makes "first edit wins" atomic: doing it in C# left two
    /// simultaneous edits able to disagree about which timestamp was the first, and whichever
    /// lost would have overwritten the other's. Deliberately NOT an EF concurrency token, which
    /// would gate deletes on this column too.
    /// </remarks>
    public async Task<bool> TryApplyEditAsync(
        Ulid conversationId,
        Ulid messageId,
        string content,
        DateTimeOffset editedAt,
        int expectedVersion,
        CancellationToken cancellationToken = default)
    {
        // Bypasses the change tracker, so the already-tracked entity (mutated by Message.Edit
        // for validation) would otherwise be flushed by the next SaveChanges and quietly undo
        // the compare-and-set. Detach it: this statement is the whole write, and the handler
        // rebuilds its response from the values it already applied in memory.
        var tracked = context.ChangeTracker.Entries<Message>()
            .Select(entry => entry.Entity)
            .FirstOrDefault(message => message.Id == messageId
                                       && message.ConversationId == conversationId);
        if (tracked is not null)
            context.Entry(tracked).State = EntityState.Detached;

        // deleted_at is repeated even though the global soft-delete filter already excludes it:
        // this statement must never be able to resurrect a deleted message, and a comment is a
        // weaker guarantee than the predicate being here.
        var affected = await context.Messages
            .Where(message => message.Id == messageId
                              && message.ConversationId == conversationId
                              && message.Version == expectedVersion
                              && message.DeletedAt == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(message => message.Content, content)
                    .SetProperty(message => message.EditedAt, message => message.EditedAt ?? editedAt)
                    .SetProperty(message => message.Version, message => message.Version + 1),
                cancellationToken);

        return affected == 1;
    }

    // ---- Reply references -------------------------------------------------------------------

    public async Task<IReadOnlyDictionary<Ulid, Message>> GetByIdsIncludingDeletedAsync(
        IReadOnlyCollection<Ulid> ids,
        CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
            return new Dictionary<Ulid, Message>();

        // Ignore the soft-delete filter only. Ignoring every query filter would also drop the
        // Blocks one, and this is reachable from a conversation that has already been proven
        // unblocked — but a query that silently ignores blocks is the exact bug class this
        // codebase keeps writing warnings about, so the filter is named rather than blanket.
        var messages = await context.Messages
            .IgnoreQueryFilters([AppDbContext.SoftDeleteFilter])
            .Where(message => ids.Contains(message.Id))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return messages.ToDictionary(message => message.Id);
    }

    // ---- Reactions -------------------------------------------------------------------------

    public Task<bool> ReactionExistsAsync(
        Ulid messageId,
        Ulid userId,
        string emoji,
        CancellationToken cancellationToken = default)
    {
        // Any() rather than a projected existence check: probing all three key columns reads
        // clearest as a single is-it-there check.
        return context.MessageReactions.AnyAsync(
            reaction => reaction.MessageId == messageId
                        && reaction.UserId == userId
                        && reaction.Emoji == emoji,
            cancellationToken);
    }

    public async Task AddReactionAsync(MessageReaction reaction, CancellationToken cancellationToken = default)
    {
        await context.MessageReactions.AddAsync(reaction, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> RemoveReactionAsync(
        Ulid messageId,
        Ulid userId,
        string emoji,
        CancellationToken cancellationToken = default)
    {
        var existing = await context.MessageReactions.FirstOrDefaultAsync(
            reaction => reaction.MessageId == messageId
                        && reaction.UserId == userId
                        && reaction.Emoji == emoji,
            cancellationToken);

        // No-op when already gone, so a repeated un-react is idempotent.
        if (existing is null)
            return false;

        context.MessageReactions.Remove(existing);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyDictionary<Ulid, IReadOnlyList<ReactionTally>>> GetReactionsAsync(
        IReadOnlyCollection<Ulid> messageIds,
        Ulid viewerId,
        CancellationToken cancellationToken = default)
    {
        if (messageIds.Count == 0)
            return new Dictionary<Ulid, IReadOnlyList<ReactionTally>>();

        // One query for the whole page. Projected to the three columns the tally needs and
        // aggregated in memory: a 20-message page carries a handful of reactions each, and
        // grouping here avoids relying on EF to translate a nested GroupBy.
        var rows = await context.MessageReactions
            .Where(reaction => messageIds.Contains(reaction.MessageId))
            .Select(reaction => new { reaction.MessageId, reaction.Emoji, reaction.UserId })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => row.MessageId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ReactionTally>)group
                    .GroupBy(row => row.Emoji)
                    .Select(emojiGroup => new ReactionTally(
                        emojiGroup.Key,
                        emojiGroup.Count(),
                        emojiGroup.Any(row => row.UserId == viewerId)))
                    // Most-reacted first; picker order breaks ties so chips do not reshuffle
                    // between two reads when two reactions tie at one.
                    .OrderByDescending(tally => tally.Count)
                    .ThenBy(tally => ReactionEmoji.OrderOf(tally.Emoji))
                    .ToList());
    }

    public async Task<ReactionTally?> GetReactionTallyAsync(
        Ulid messageId,
        string emoji,
        Ulid viewerId,
        CancellationToken cancellationToken = default)
    {
        // No AsNoTracking: this projects to a scalar, which is never tracked anyway, and
        // EF's overload there requires an entity type.
        var reactors = await context.MessageReactions
            .Where(reaction => reaction.MessageId == messageId && reaction.Emoji == emoji)
            .Select(reaction => reaction.UserId)
            .ToListAsync(cancellationToken);

        // Null means nobody has this reaction — including the case where the toggle just
        // removed the viewer's own and took the count to zero. Callers treat null as "drop
        // the chip entirely" rather than rendering a zero.
        if (reactors.Count == 0)
            return null;

        return new ReactionTally(emoji, reactors.Count, reactors.Contains(viewerId));
    }

    public async Task RemoveReactionsForMessageAsync(Ulid messageId, CancellationToken cancellationToken = default)
    {
        await context.MessageReactions
            .Where(reaction => reaction.MessageId == messageId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public void StageReactionRemoval(Ulid messageId)
    {
        // No save: the caller's own write commits this alongside it, so a moderator's reaction
        // removal lands in the same transaction as their audit entry (§10.3). MessageReaction is
        // not ISoftDeletable, so Remove here is a real delete and the soft-delete interceptor
        // leaves it alone. Synced enumeration, matching the synchronous staging contract.
        context.MessageReactions.RemoveRange(
            context.MessageReactions.Where(reaction => reaction.MessageId == messageId));
    }
}
