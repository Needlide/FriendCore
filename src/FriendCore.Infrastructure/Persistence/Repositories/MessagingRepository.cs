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

    public async Task AddConversationAsync(Conversation conversation, CancellationToken cancellationToken = default)
    {
        await context.Conversations.AddAsync(conversation, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
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
        return context.Messages.FirstOrDefaultAsync(
            message => message.Id == messageId && message.ConversationId == conversationId,
            cancellationToken);
    }

    public async Task SoftDeleteAsync(
        Message message,
        DateTimeOffset deletedAt,
        CancellationToken cancellationToken = default)
    {
        // Deliberately not context.Messages.Remove(message): the interceptor would stamp
        // DeletedAt and nothing else, leaving the text readable in the row. MarkDeleted is
        // what scrubs it. The row itself is never hard-deleted here — §2.3 soft deletes.
        message.MarkDeleted(deletedAt);
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task<Message?> GetMessageForModerationAsync(Ulid messageId, CancellationToken cancellationToken = default)
    {
        return context.Messages
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(message => message.Id == messageId, cancellationToken);
    }

    public void StageSoftDelete(Message message, DateTimeOffset deletedAt)
    {
        // No save here: the moderation audit write commits the removal alongside its log entry.
        // Goes through MarkDeleted rather than context.Messages.Remove so the text is scrubbed
        // on this path too — Remove alone would only stamp DeletedAt and leave the words behind.
        message.MarkDeleted(deletedAt);
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

    public async Task<IReadOnlyList<Message>> GetMessagesAsync(
        Ulid conversationId,
        (DateTimeOffset CreatedAt, Ulid Id)? cursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var query = context.Messages.Where(message => message.ConversationId == conversationId);

        if (cursor is { } before)
            query = query.Where(message =>
                message.CreatedAt < before.CreatedAt ||
                (message.CreatedAt == before.CreatedAt && message.Id.CompareTo(before.Id) < 0));

        return await query
            .OrderByDescending(message => message.CreatedAt)
            .ThenByDescending(message => message.Id)
            .Take(limit)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
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
                       && (lastReadAt == null || message.CreatedAt > lastReadAt),
            cancellationToken);
    }
}
