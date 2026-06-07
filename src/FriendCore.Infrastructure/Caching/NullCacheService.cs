using FriendCore.Application.Common.Interfaces;

namespace FriendCore.Infrastructure.Caching;

// Fallback when no Redis connection is configured (e.g. tests, local runs without Redis).
// Always misses and never persists, so callers transparently fall back to their source of truth.
public sealed class NullCacheService : ICacheService
{
    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult<T?>(default);

    public Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<T> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? ttl = null,
        CancellationToken cancellationToken = default) =>
        factory(cancellationToken);
}
