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

        // The message junction is a plain Ulid column, so ANY(...) works here unlike the old
        // nullable messages.media_item_id. Counting the link row (not the message) also keeps
        // soft-deleted messages' images spoken for, which is what stops a re-upload of the same
        // media being attached to a new message.
        var inMessages = await context.MessageMedia
            .Where(link => ids.Contains(link.MediaItemId))
            .Select(link => link.MediaItemId)
            .ToListAsync(cancellationToken);

        // Union across both link tables.
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

        // The message junction is a plain Ulid column, so ANY(...) is safe here. The join to
        // messages keeps the soft-delete filter: an image whose message was erased is no longer
        // revealable through the attachment path.
        var message = await context.MessageMedia
            .Where(link => link.MediaItemId == mediaId)
            .Join(
                context.Messages,
                link => link.MessageId,
                candidate => candidate.Id,
                (link, candidate) => new { candidate.Id, candidate.ConversationId })
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
