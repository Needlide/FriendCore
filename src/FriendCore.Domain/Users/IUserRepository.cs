namespace FriendCore.Domain.Users;

public interface IUserRepository
{
    // Email-hash lookups: parameters are HMAC hex strings, never plaintext addresses.
    Task<bool> ExistsByEmailHashAsync(string emailHash, CancellationToken cancellationToken = default);
    Task<bool> ExistsByUsernameSlugAsync(string usernameSlug, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailHashAsync(string emailHash, CancellationToken cancellationToken = default);
    Task<User?> GetByIdAsync(Ulid id, CancellationToken cancellationToken = default);
    Task<User?> GetByUsernameSlugAsync(string usernameSlug, CancellationToken cancellationToken = default);

    // Resolves the user even when a block relationship hides them from normal queries. Only for
    // block management itself (block/unblock must reach the counterpart across the block).
    Task<User?> GetByUsernameSlugIgnoringBlocksAsync(string usernameSlug, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<User>> GetByIdsAsync(IReadOnlyCollection<Ulid> ids, CancellationToken cancellationToken = default);

    // Like GetByIdsAsync but ignores the block filter — moderation views must show every user
    // regardless of the acting moderator's own block relationships.
    Task<IReadOnlyList<User>> GetByIdsIgnoringBlocksAsync(
        IReadOnlyCollection<Ulid> ids,
        CancellationToken cancellationToken = default);

    // Tracked, block-ignoring single load for moderation enforcement (suspend/ban mutate it).
    Task<User?> GetByIdIgnoringBlocksAsync(Ulid id, CancellationToken cancellationToken = default);

    // Oldest Admin account — staffs the support inbox. Block filter is ignored so the help
    // channel stays reachable even across a (legacy) block relationship.
    Task<User?> GetFirstAdminAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<User>> GetUsersWithProfileTagAsync(Ulid tagId, CancellationToken cancellationToken = default);
    Task<UserPreferences?> GetPreferencesAsync(Ulid userId, CancellationToken cancellationToken = default);
    Task AddAsync(User user, UserPreferences preferences, CancellationToken cancellationToken = default);
    Task UpdateAsync(User user, CancellationToken cancellationToken = default);
    Task UpdatePreferencesAsync(CancellationToken cancellationToken = default);
}