using FriendCore.Domain.Posts;
using Microsoft.EntityFrameworkCore;

namespace FriendCore.Infrastructure.Persistence.Repositories;

public sealed class PostRepository(AppDbContext context) : IPostRepository
{
    public Task<Post?> GetByIdAsync(Ulid id, CancellationToken cancellationToken = default)
    {
        return context.Posts
            .Include(post => post.Media)
            .FirstOrDefaultAsync(post => post.Id == id, cancellationToken);
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
        (DateTimeOffset CreatedAt, Ulid Id)? cursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var query = context.Posts.Where(post => post.AuthorId == authorId);

        if (cursor is { } before)
            query = query.Where(post =>
                post.CreatedAt < before.CreatedAt ||
                (post.CreatedAt == before.CreatedAt && post.Id.CompareTo(before.Id) < 0));

        return await query
            .OrderByDescending(post => post.CreatedAt)
            .ThenByDescending(post => post.Id)
            .Take(limit)
            .Include(post => post.Media)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Post>> GetHomeFeedAsync(
        Ulid userId,
        IReadOnlyList<string> keywordFilters,
        (DateTimeOffset CreatedAt, Ulid Id)? cursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var query = context.Posts.Where(post =>
            context.Follows.Any(follow => follow.FollowerId == userId && follow.FollowedId == post.AuthorId)
            // One-way mute (§13.6): excluded from the feed only — deliberately not a global
            // filter, so direct profile and post access stay visible.
            && !context.Mutes.Any(mute => mute.MuterId == userId && mute.MutedId == post.AuthorId));

        // Filtering happens in SQL, before the cursor cut, so pages stay full and the cursor
        // never points at a hidden post. Media-only posts (null content) are never keyword-hidden.
        if (keywordFilters.Count > 0)
            query = query.Where(post =>
                post.Content == null
                || !keywordFilters.Any(keyword => EF.Functions.ILike(post.Content!, "%" + keyword + "%")));

        if (cursor is { } before)
            query = query.Where(post =>
                post.CreatedAt < before.CreatedAt ||
                (post.CreatedAt == before.CreatedAt && post.Id.CompareTo(before.Id) < 0));

        return await query
            .OrderByDescending(post => post.CreatedAt)
            .ThenByDescending(post => post.Id)
            .Take(limit)
            .Include(post => post.Media)
            .AsNoTracking()
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
            .AsNoTracking()
            .Select(like => like.PostId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Ulid>> GetAttachedMediaIdsAsync(
        IReadOnlyCollection<Ulid> mediaIds,
        CancellationToken cancellationToken = default)
    {
        if (mediaIds.Count == 0)
            return [];

        // PostMedia has no soft-delete filter, so attachments to soft-deleted posts count too —
        // matching the unique index, which spans them.
        return await context.PostMedia
            .Where(postMedia => mediaIds.Contains(postMedia.MediaItemId))
            .Select(postMedia => postMedia.MediaItemId)
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
        (DateTimeOffset CreatedAt, Ulid Id)? cursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var query = context.Comments.Where(comment => comment.PostId == postId);

        if (cursor is { } before)
            query = query.Where(comment =>
                comment.CreatedAt < before.CreatedAt ||
                (comment.CreatedAt == before.CreatedAt && comment.Id.CompareTo(before.Id) < 0));

        return await query
            .OrderByDescending(comment => comment.CreatedAt)
            .ThenByDescending(comment => comment.Id)
            .Take(limit)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}