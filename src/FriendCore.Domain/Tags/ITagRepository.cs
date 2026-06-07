namespace FriendCore.Domain.Tags;

public interface ITagRepository
{
    Task<Tag?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);

    Task<Tag?> GetByIdAsync(Ulid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Tag>> GetByIdsAsync(IReadOnlyCollection<Ulid> ids,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Tag>> SearchActiveByPrefixAsync(
        string slugPrefix,
        int limit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Tag>> GetByStatusAsync(
        TagStatus status,
        int limit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Tag>> GetArchivableAsync(
        int maxProfileCountExclusive,
        DateTimeOffset lastUsedBefore,
        CancellationToken cancellationToken = default);

    Task AddAsync(Tag tag, CancellationToken cancellationToken = default);

    Task UpdateAsync(CancellationToken cancellationToken = default);
}