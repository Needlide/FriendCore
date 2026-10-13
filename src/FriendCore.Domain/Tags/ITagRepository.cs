namespace FriendCore.Domain.Tags;

public interface ITagRepository
{
    Task<Tag?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);

    // Resolves an alias: if the slug maps to a Merged tag, returns its merge target instead.
    Task<Tag?> GetBySlugResolvingAliasAsync(string slug, CancellationToken cancellationToken = default);

    Task<Tag?> GetByIdAsync(Ulid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Tag>> GetByIdsAsync(IReadOnlyCollection<Ulid> ids,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Tag>> SearchUsableByPrefixAsync(
        string slugPrefix,
        int limit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Tag>> GetByStatusAsync(
        TagStatus status,
        int limit,
        CancellationToken cancellationToken = default);

    // Returns up to batchSize stale tags. The auto-archive sweep drains by calling this
    // repeatedly: archived tags leave the filter, so there is no offset to advance.
    Task<IReadOnlyList<Tag>> GetArchivableAsync(
        int maxProfileCountExclusive,
        DateTimeOffset lastUsedBefore,
        int batchSize,
        CancellationToken cancellationToken = default);

    Task AddAsync(Tag tag, CancellationToken cancellationToken = default);

    Task AddRangeAsync(IEnumerable<Tag> tags, CancellationToken cancellationToken = default);

    Task UpdateAsync(CancellationToken cancellationToken = default);
}