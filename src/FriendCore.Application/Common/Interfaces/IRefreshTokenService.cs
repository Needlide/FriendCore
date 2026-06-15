using ErrorOr;

namespace FriendCore.Application.Common.Interfaces;

/// <summary>
///     Issues, rotates, and revokes refresh tokens (§10.1). The raw token is returned only here
///     (the API places it in an HttpOnly cookie); only its hash is persisted.
/// </summary>
public interface IRefreshTokenService
{
    Task<IssuedRefreshToken> IssueAsync(Ulid userId, CancellationToken cancellationToken = default);

    // Validates the presented token and rotates it (revoke old, issue new). Reuse of an
    // already-revoked token revokes the user's whole family and fails.
    Task<ErrorOr<RotatedRefreshToken>> RotateAsync(string rawToken, CancellationToken cancellationToken = default);

    // Logout: revoke the presented token. Idempotent — unknown/expired tokens are a no-op.
    Task RevokeAsync(string rawToken, CancellationToken cancellationToken = default);

    // Revoke every active token for a user (e.g. when a refresh is rejected for a banned account).
    Task RevokeAllForUserAsync(Ulid userId, CancellationToken cancellationToken = default);
}

public sealed record IssuedRefreshToken(string Token, DateTimeOffset ExpiresAt);

public sealed record RotatedRefreshToken(Ulid UserId, string Token, DateTimeOffset ExpiresAt);
