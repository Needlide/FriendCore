using FriendCore.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace FriendCore.Infrastructure.Persistence.Repositories;

public sealed class MuteRepository(AppDbContext context) : IMuteRepository
{
    public Task<bool> ExistsAsync(Ulid muterId, Ulid mutedId, CancellationToken cancellationToken = default)
    {
        return context.Mutes.AnyAsync(
            mute => mute.MuterId == muterId && mute.MutedId == mutedId,
            cancellationToken);
    }

    public async Task AddAsync(Mute mute, CancellationToken cancellationToken = default)
    {
        await context.Mutes.AddAsync(mute, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> RemoveAsync(Ulid muterId, Ulid mutedId, CancellationToken cancellationToken = default)
    {
        var mute = await context.Mutes.FirstOrDefaultAsync(
            m => m.MuterId == muterId && m.MutedId == mutedId,
            cancellationToken);

        if (mute is null)
            return false;

        context.Mutes.Remove(mute);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
