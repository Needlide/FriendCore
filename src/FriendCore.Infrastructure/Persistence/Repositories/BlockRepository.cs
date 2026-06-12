using FriendCore.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace FriendCore.Infrastructure.Persistence.Repositories;

public sealed class BlockRepository(AppDbContext context) : IBlockRepository
{
    public Task<bool> ExistsAsync(Ulid blockerId, Ulid blockedId, CancellationToken cancellationToken = default)
    {
        return context.Blocks.AnyAsync(
            block => block.BlockerId == blockerId && block.BlockedId == blockedId,
            cancellationToken);
    }

    public Task<bool> ExistsBetweenAsync(Ulid userA, Ulid userB, CancellationToken cancellationToken = default)
    {
        return context.Blocks.AnyAsync(
            block => (block.BlockerId == userA && block.BlockedId == userB)
                     || (block.BlockerId == userB && block.BlockedId == userA),
            cancellationToken);
    }

    public async Task AddAndRemoveFollowsAsync(Block block, CancellationToken cancellationToken = default)
    {
        var follows = await context.Follows
            .Where(follow =>
                (follow.FollowerId == block.BlockerId && follow.FollowedId == block.BlockedId)
                || (follow.FollowerId == block.BlockedId && follow.FollowedId == block.BlockerId))
            .ToListAsync(cancellationToken);

        context.Follows.RemoveRange(follows);
        await context.Blocks.AddAsync(block, cancellationToken);

        // Single SaveChanges = one transaction: the block never lands without the follow sever.
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> RemoveAsync(Ulid blockerId, Ulid blockedId, CancellationToken cancellationToken = default)
    {
        var block = await context.Blocks.FirstOrDefaultAsync(
            b => b.BlockerId == blockerId && b.BlockedId == blockedId,
            cancellationToken);

        if (block is null)
            return false;

        context.Blocks.Remove(block);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
