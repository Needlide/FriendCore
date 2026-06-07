using FriendCore.Domain.Tags;
using Microsoft.EntityFrameworkCore;

namespace FriendCore.Infrastructure.Persistence.Repositories;

public sealed class TagRepository(AppDbContext context) : ITagRepository
{
    public Task<Tag?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        return context.Tags.FirstOrDefaultAsync(tag => tag.Slug == slug, cancellationToken);
    }

    public Task<Tag?> GetByIdAsync(Ulid id, CancellationToken cancellationToken = default)
    {
        return context.Tags.FirstOrDefaultAsync(tag => tag.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Tag>> GetByIdsAsync(
        IReadOnlyCollection<Ulid> ids,
        CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
            return [];

        return await context.Tags
            .Where(tag => ids.Contains(tag.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Tag>> SearchActiveByPrefixAsync(
        string slugPrefix,
        int limit,
        CancellationToken cancellationToken = default)
    {
        return await context.Tags
            .Where(tag => tag.Status == TagStatus.Active && tag.Slug.StartsWith(slugPrefix))
            .OrderByDescending(tag => tag.TotalCount)
            .ThenBy(tag => tag.Slug)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Tag>> GetByStatusAsync(
        TagStatus status,
        int limit,
        CancellationToken cancellationToken = default)
    {
        return await context.Tags
            .Where(tag => tag.Status == status)
            .OrderBy(tag => tag.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Tag>> GetArchivableAsync(
        int maxProfileCountExclusive,
        DateTimeOffset lastUsedBefore,
        CancellationToken cancellationToken = default)
    {
        return await context.Tags
            .Where(tag =>
                (tag.Status == TagStatus.Active || tag.Status == TagStatus.PendingReview) &&
                tag.ProfileCount < maxProfileCountExclusive &&
                tag.LastUsedAt < lastUsedBefore)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Tag tag, CancellationToken cancellationToken = default)
    {
        await context.Tags.AddAsync(tag, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task UpdateAsync(CancellationToken cancellationToken = default)
    {
        return context.SaveChangesAsync(cancellationToken);
    }
}