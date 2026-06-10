using FriendCore.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace FriendCore.Infrastructure.Persistence.Repositories;

public sealed class FollowRepository(AppDbContext context) : IFollowRepository
{
    public Task<bool> ExistsAsync(Ulid followerId, Ulid followedId, CancellationToken cancellationToken = default)
    {
        return context.Follows.AnyAsync(
            follow => follow.FollowerId == followerId && follow.FollowedId == followedId,
            cancellationToken);
    }

    public async Task AddAsync(Follow follow, CancellationToken cancellationToken = default)
    {
        await context.Follows.AddAsync(follow, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> RemoveAsync(Ulid followerId, Ulid followedId, CancellationToken cancellationToken = default)
    {
        var follow = await context.Follows.FirstOrDefaultAsync(
            f => f.FollowerId == followerId && f.FollowedId == followedId,
            cancellationToken);

        if (follow is null)
            return false;

        context.Follows.Remove(follow);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<User>> GetFollowersAsync(
        Ulid userId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        return await (
                from follow in context.Follows
                where follow.FollowedId == userId
                join user in context.Users on follow.FollowerId equals user.Id
                orderby follow.CreatedAt descending
                select user)
            .Take(limit)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<User>> GetFollowingAsync(
        Ulid userId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        return await (
                from follow in context.Follows
                where follow.FollowerId == userId
                join user in context.Users on follow.FollowedId equals user.Id
                orderby follow.CreatedAt descending
                select user)
            .Take(limit)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}