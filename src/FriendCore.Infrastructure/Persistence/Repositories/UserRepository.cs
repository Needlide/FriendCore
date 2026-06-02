using FriendCore.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace FriendCore.Infrastructure.Persistence.Repositories;

public sealed class UserRepository(AppDbContext context) : IUserRepository
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

    public async Task AddAsync(User user, UserPreferences preferences, CancellationToken cancellationToken = default)
    {
        await context.Users.AddAsync(user, cancellationToken);
        await context.UserPreferences.AddAsync(preferences, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        context.Users.Update(user);
        await context.SaveChangesAsync(cancellationToken);
    }
}