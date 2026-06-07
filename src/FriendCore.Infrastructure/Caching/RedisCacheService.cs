using System.Text.Json;
using FriendCore.Application.Common.Interfaces;
using StackExchange.Redis;

namespace FriendCore.Infrastructure.Caching;

public sealed class RedisCacheService(IConnectionMultiplexer connection) : ICacheService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private IDatabase Database => connection.GetDatabase();

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var value = await Database.StringGetAsync(key);
        return value.IsNullOrEmpty ? default : JsonSerializer.Deserialize<T>((string)value!, SerializerOptions);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
    {
        var serialized = JsonSerializer.Serialize(value, SerializerOptions);
        return Database.StringSetAsync(key, serialized, ttl);
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        return Database.KeyDeleteAsync(key);
    }

    public async Task<T> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? ttl = null,
        CancellationToken cancellationToken = default)
    {
        var cached = await GetAsync<T>(key, cancellationToken);
        if (cached is not null)
            return cached;

        var value = await factory(cancellationToken);
        await SetAsync(key, value, ttl, cancellationToken);
        return value;
    }
}
