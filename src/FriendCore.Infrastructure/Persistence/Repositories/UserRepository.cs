using FriendCore.Application.Common.Interfaces;
using FriendCore.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace FriendCore.Infrastructure.Persistence.Repositories;

public sealed class UserRepository(AppDbContext context, IDomainEventDispatcher domainEventDispatcher)
    : IUserRepository
{
    public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return context.Users.AnyAsync(user => user.Email == email, cancellationToken);
    }

    public Task<bool> ExistsByUsernameSlugAsync(string usernameSlug, CancellationToken cancellationToken = default)
    {
        return context.Users.AnyAsync(user => user.UsernameSlug == usernameSlug, cancellationToken);
    }

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return context.Users.FirstOrDefaultAsync(user => user.Email == email, cancellationToken);
    }

    public Task<User?> GetByIdAsync(Ulid id, CancellationToken cancellationToken = default)
    {
        return context.Users.FirstOrDefaultAsync(user => user.Id == id, cancellationToken);
    }

    public Task<User?> GetByUsernameSlugAsync(string usernameSlug, CancellationToken cancellationToken = default)
    {
        return context.Users.FirstOrDefaultAsync(user => user.UsernameSlug == usernameSlug, cancellationToken);
    }

    public Task<User?> GetByUsernameSlugIgnoringBlocksAsync(
        string usernameSlug,
        CancellationToken cancellationToken = default)
    {
        // Skips only the block filter — soft-delete still applies.
        return context.Users
            .IgnoreQueryFilters([AppDbContext.BlocksFilter])
            .FirstOrDefaultAsync(user => user.UsernameSlug == usernameSlug, cancellationToken);
    }

    public async Task<IReadOnlyList<User>> GetByIdsAsync(
        IReadOnlyCollection<Ulid> ids,
        CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
            return [];

        return await context.Users.Where(user => ids.Contains(user.Id)).AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<User>> GetByIdsIgnoringBlocksAsync(
        IReadOnlyCollection<Ulid> ids,
        CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
            return [];

        // Skips only the block filter — soft-delete still applies.
        return await context.Users
            .IgnoreQueryFilters([AppDbContext.BlocksFilter])
            .Where(user => ids.Contains(user.Id))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public Task<User?> GetByIdIgnoringBlocksAsync(Ulid id, CancellationToken cancellationToken = default)
    {
        // Tracked (no AsNoTracking) so moderation enforcement can mutate the loaded user.
        return context.Users
            .IgnoreQueryFilters([AppDbContext.BlocksFilter])
            .FirstOrDefaultAsync(user => user.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<User>> GetUsersWithProfileTagAsync(
        Ulid tagId,
        CancellationToken cancellationToken = default)
    {
        return await context.Users
            .Where(user => user.ProfileTags.Any(tag => tag.TagId == tagId))
            .ToListAsync(cancellationToken);
    }

    public Task<UserPreferences?> GetPreferencesAsync(Ulid userId, CancellationToken cancellationToken = default)
    {
        return context.UserPreferences.FirstOrDefaultAsync(
            preferences => preferences.UserId == userId,
            cancellationToken);
    }

    public async Task AddAsync(User user, UserPreferences preferences, CancellationToken cancellationToken = default)
    {
        await context.Users.AddAsync(user, cancellationToken);
        await context.UserPreferences.AddAsync(preferences, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        await domainEventDispatcher.DispatchEventsAsync(cancellationToken);
    }

    public async Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
        await domainEventDispatcher.DispatchEventsAsync(cancellationToken);
    }

    public Task UpdatePreferencesAsync(CancellationToken cancellationToken = default)
    {
        return context.SaveChangesAsync(cancellationToken);
    }
}