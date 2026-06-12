namespace FriendCore.Domain.Users;

public interface IMuteRepository
{
    Task<bool> ExistsAsync(Ulid muterId, Ulid mutedId, CancellationToken cancellationToken = default);

    Task AddAsync(Mute mute, CancellationToken cancellationToken = default);

    Task<bool> RemoveAsync(Ulid muterId, Ulid mutedId, CancellationToken cancellationToken = default);
}
