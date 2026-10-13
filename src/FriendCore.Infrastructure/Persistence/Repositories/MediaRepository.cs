using FriendCore.Domain.Media;
using FriendCore.Domain.Messaging;
using Microsoft.EntityFrameworkCore;

namespace FriendCore.Infrastructure.Persistence.Repositories;

public sealed class MediaRepository(AppDbContext context) : IMediaRepository
{
    public Task<MediaItem?> GetByIdAsync(Ulid id, CancellationToken cancellationToken = default)
    {
        // Variants are owned types — loaded with the owner, no Include needed.
        return context.MediaItems.FirstOrDefaultAsync(media => media.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<MediaItem>> GetByIdsAsync(
        IReadOnlyCollection<Ulid> ids,
        CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
            return [];

        return await context.MediaItems
            .Where(media => ids.Contains(media.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(MediaItem media, CancellationToken cancellationToken = default)
    {
        await context.MediaItems.AddAsync(media, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Ulid>> GetAttachedIdsAsync(
        IReadOnlyCollection<Ulid> ids,
        CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
            return [];

        var inPosts = await context.PostMedia
            .Where(link => ids.Contains(link.MediaItemId))
            .Select(link => link.MediaItemId)
            .ToListAsync(cancellationToken);

        // Message.MediaItemId is a NULLABLE Ulid, and Npgsql cannot build the ANY(...)
        // parameter for a Ulid?[] — it hunts for a JSON array mapping and throws before
        // the query ever runs. Per-id equality is a plain indexed compare with a scalar
        // parameter, which the conversion handles fine. A message carries at most one
        // attachment and a post at most a handful, so this stays a handful of lookups.
        var inMessages = new List<Ulid>();
        foreach (var id in ids)
        {
            if (await context.Messages.AnyAsync(
                    message => message.MediaItemId == (Ulid?)id, cancellationToken))
                inMessages.Add(id);
        }

        // Union across both link tables, and soft-deleted owners still count: a deleted
        // post's media stays in the table, so treating it as free would let the same
        // upload be re-attached while the old row still references it.
        return inPosts.Concat(inMessages).Distinct().ToList();
    }

    public async Task<IReadOnlyList<Ulid>> GetStuckProcessingIdsAsync(
        DateTimeOffset stuckBefore,
        int limit,
        CancellationToken cancellationToken = default)
    {
        return await context.MediaItems
            .Where(media => media.Status == MediaStatus.Processing && media.CreatedAt <= stuckBefore)
            .OrderBy(media => media.CreatedAt)
            .Take(limit)
            .Select(media => media.Id)
            .ToListAsync(cancellationToken);
    }

    public Task UpdateAsync(CancellationToken cancellationToken = default)
    {
        return context.SaveChangesAsync(cancellationToken);
    }

    public async Task SoftDeleteAsync(MediaItem media, CancellationToken cancellationToken = default)
    {
        // The SoftDeleteInterceptor converts this Remove into a DeletedAt stamp.
        context.MediaItems.Remove(media);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<MediaAttachment> GetAttachmentAsync(
        Ulid mediaId,
        CancellationToken cancellationToken = default)
    {
        // Post first: a media item is attachable exactly once, so at most one of these can hit.
        var postId = await context.PostMedia
            .Where(link => link.MediaItemId == mediaId)
            .Select(link => link.PostId)
            .FirstOrDefaultAsync(cancellationToken);
        if (postId != Ulid.Empty)
            return new MediaAttachment(postId, null, null);

        // Message.MediaItemId is a nullable Ulid, which Npgsql cannot use in an ANY(...) array —
        // the same trap documented on GetAttachedIdsAsync. Plain equality on a scalar avoids it.
        var message = await context.Messages
            .Where(m => m.MediaItemId == mediaId)
            .Select(m => new { m.Id, m.ConversationId })
            .FirstOrDefaultAsync(cancellationToken);

        return message is null
            ? MediaAttachment.Unattached
            : new MediaAttachment(null, message.Id, message.ConversationId);
    }

    public Task<MediaItem?> GetByIdForModerationAsync(Ulid id, CancellationToken cancellationToken = default)
    {
        return context.MediaItems
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(media => media.Id == id, cancellationToken);
    }

    public void StageSoftDelete(MediaItem media)
    {
        // No save here: the moderation audit write commits the removal alongside its log entry,
        // so no action can persist without an audit record (§10.3).
        context.MediaItems.Remove(media);
    }

    // ---- Consent refusals ---------------------------------------------------------------------
    // Refusals are never soft-deleted, so no query filter applies to this table. Nothing else
    // may add one: the one failure this lookup must never have is quietly returning nothing.

    public async Task<IReadOnlyList<string>> GetRefusedHashesAsync(
        Ulid senderId,
        Ulid recipientId,
        IReadOnlyCollection<string> contentHashes,
        CancellationToken cancellationToken = default)
    {
        if (contentHashes.Count == 0)
            return [];

        return await context.MediaRefusals
            .Where(refusal => refusal.RecipientId == recipientId
                              && refusal.SenderId == senderId
                              && contentHashes.Contains(refusal.ContentHash))
            .Select(refusal => refusal.ContentHash)
            .ToListAsync(cancellationToken);
    }
}
