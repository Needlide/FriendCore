namespace FriendCore.Domain.Users;

public interface IRefreshTokenRepository
{
    // Tracked lookup by hash (returns the token in any state, so reuse of a revoked token is
    // detectable). Null if no such token exists.
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    // Adds the token and commits — also flushes any tracked revocation staged in the same unit
    // of work (so rotation's revoke-old + add-new land together).
    Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    // Revokes every still-active token for a user (logout-everywhere / reuse detection / on a
    // ban or suspension).
    Task RevokeAllActiveForUserAsync(Ulid userId, DateTimeOffset at, CancellationToken cancellationToken = default);
}
