using FriendCore.Application.Common.Interfaces;

namespace FriendCore.IntegrationTests.Infrastructure;

/// <summary>
///     Controllable clock so tests can advance time past a token's expiry. Defaults to the real
///     wall clock until <see cref="Set" /> is called.
/// </summary>
public sealed class MutableDateTimeProvider : IDateTimeProvider
{
    private DateTimeOffset? _override;

    public DateTimeOffset UtcNow => _override ?? DateTimeOffset.UtcNow;

    public void Set(DateTimeOffset value) => _override = value;

    public void Reset() => _override = null;
}
