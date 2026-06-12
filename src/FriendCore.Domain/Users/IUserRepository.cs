namespace FriendCore.Domain.Users;

public interface IUserRepository
{
    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> ExistsByUsernameSlugAsync(string usernameSlug, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<User?> GetByIdAsync(Ulid id, CancellationToken cancellationToken = default);
    Task<User?> GetByUsernameSlugAsync(string usernameSlug, CancellationToken cancellationToken = default);

    // Resolves the user even when a block relationship hides them from normal queries. Only for
    // block management itself (block/unblock must reach the counterpart across the block).
    Task<User?> GetByUsernameSlugIgnoringBlocksAsync(string usernameSlug, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<User>> GetByIdsAsync(IReadOnlyCollection<Ulid> ids, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<User>> GetUsersWithProfileTagAsync(Ulid tagId, CancellationToken cancellationToken = default);
    Task AddAsync(User user, UserPreferences preferences, CancellationToken cancellationToken = default);
    Task UpdateAsync(User user, CancellationToken cancellationToken = default);
}