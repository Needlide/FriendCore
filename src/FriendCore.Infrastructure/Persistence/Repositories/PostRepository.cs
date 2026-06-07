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

    public Task<bool> LikeExistsAsync(Ulid postId, Ulid userId, CancellationToken cancellationToken = default)
    {
        return context.Likes.AnyAsync(
            like => like.PostId == postId && like.UserId == userId,
            cancellationToken);
    }

    public async Task AddLikeAsync(Like like, CancellationToken cancellationToken = default)
    {
        await context.Likes.AddAsync(like, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> RemoveLikeAsync(Ulid postId, Ulid userId, CancellationToken cancellationToken = default)
    {
        var like = await context.Likes.FirstOrDefaultAsync(
            l => l.PostId == postId && l.UserId == userId,
            cancellationToken);

        if (like is null)
            return false;

        context.Likes.Remove(like);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<Ulid>> GetLikedPostIdsAsync(
        Ulid userId,
        IReadOnlyCollection<Ulid> postIds,
        CancellationToken cancellationToken = default)
    {
        if (postIds.Count == 0)
            return [];

        return await context.Likes
            .Where(like => like.UserId == userId && postIds.Contains(like.PostId))
            .Select(like => like.PostId)
            .ToListAsync(cancellationToken);
    }

    public async Task AddCommentAsync(Comment comment, CancellationToken cancellationToken = default)
    {
        await context.Comments.AddAsync(comment, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task<Comment?> GetCommentByIdAsync(Ulid id, CancellationToken cancellationToken = default)
    {
        return context.Comments.FirstOrDefaultAsync(comment => comment.Id == id, cancellationToken);
    }

    public async Task SoftDeleteCommentAsync(Comment comment, CancellationToken cancellationToken = default)
    {
        context.Comments.Remove(comment);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Comment>> GetCommentsByPostAsync(
        Ulid postId,
        DateTimeOffset? cursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var query = context.Comments.Where(comment => comment.PostId == postId);

        if (cursor is { } before)
            query = query.Where(comment => comment.CreatedAt < before);

        return await query
            .OrderByDescending(comment => comment.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }
}
