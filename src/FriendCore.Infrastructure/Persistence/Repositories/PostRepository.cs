using FriendCore.Domain.Posts;
using Microsoft.EntityFrameworkCore;

namespace FriendCore.Infrastructure.Persistence.Repositories;

public sealed class PostRepository(AppDbContext context) : IPostRepository
{
    public Task<Post?> GetByIdAsync(Ulid id, CancellationToken cancellationToken = default)
    {
        return context.Posts.FirstOrDefaultAsync(post => post.Id == id, cancellationToken);
    }

    public async Task AddAsync(Post post, CancellationToken cancellationToken = default)
    {
        await context.Posts.AddAsync(post, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task UpdateAsync(CancellationToken cancellationToken = default)
    {
        return context.SaveChangesAsync(cancellationToken);
    }

    public async Task SoftDeleteAsync(Post post, CancellationToken cancellationToken = default)
    {
        // The SoftDeleteInterceptor converts this Remove into a DeletedAt stamp.
        context.Posts.Remove(post);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Post>> GetByAuthorAsync(
        Ulid authorId,
        DateTimeOffset? cursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var query = context.Posts.Where(post => post.AuthorId == authorId);

        if (cursor is { } before)
            query = query.Where(post => post.CreatedAt < before);

        return await query
            .OrderByDescending(post => post.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }
}
