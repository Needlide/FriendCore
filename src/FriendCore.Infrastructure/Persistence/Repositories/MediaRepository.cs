using FriendCore.Domain.Media;
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
}
