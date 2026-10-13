using FriendCore.Domain.Tags;
using Microsoft.EntityFrameworkCore;

namespace FriendCore.Infrastructure.Persistence.Repositories;

public sealed class TagRepository(AppDbContext context) : ITagRepository
{
    public Task<Tag?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        return context.Tags.FirstOrDefaultAsync(tag => tag.Slug == slug, cancellationToken);
    }

    public async Task<Tag?> GetBySlugResolvingAliasAsync(string slug, CancellationToken cancellationToken = default)
    {
        var tag = await context.Tags.FirstOrDefaultAsync(t => t.Slug == slug, cancellationToken);
        if (tag is { Status: TagStatus.Merged, MergedIntoId: { } targetId })
            return await GetByIdAsync(targetId, cancellationToken);

        return tag;
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

    public async Task<IReadOnlyList<Tag>> SearchUsableByPrefixAsync(
        string slugPrefix,
        int limit,
        CancellationToken cancellationToken = default)
    {
        // PendingReview counts as usable, matching the rule already applied when
        // tags are read elsewhere (GetByIdsForDisplayAsync above). Tags are CREATED
        // as PendingReview, so filtering on Active alone made this return nothing for
        // every tag that had not yet been through moderation — which, without an
        // active moderation pass, is all of them. Autocomplete then offered no
        // suggestions and the UI fell through to "you're the first" on every keystroke.
        return await context.Tags
            .Where(tag => (tag.Status == TagStatus.Active || tag.Status == TagStatus.PendingReview)
                          && tag.Slug.StartsWith(slugPrefix))
            .OrderByDescending(tag => tag.TotalCount)
            .ThenBy(tag => tag.Slug)
            .Take(limit)
            .AsNoTracking()
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
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Tag>> GetArchivableAsync(
        int maxProfileCountExclusive,
        DateTimeOffset lastUsedBefore,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        return await context.Tags
            .Where(tag =>
                (tag.Status == TagStatus.Active || tag.Status == TagStatus.PendingReview) &&
                tag.ProfileCount < maxProfileCountExclusive &&
                tag.LastUsedAt < lastUsedBefore)
            .OrderBy(tag => tag.LastUsedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Tag tag, CancellationToken cancellationToken = default)
    {
        await context.Tags.AddAsync(tag, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<Tag> tags, CancellationToken cancellationToken = default)
    {
        await context.Tags.AddRangeAsync(tags, cancellationToken);
    }

    public Task UpdateAsync(CancellationToken cancellationToken = default)
    {
        return context.SaveChangesAsync(cancellationToken);
    }
}