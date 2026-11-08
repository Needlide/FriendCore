using FriendCore.Domain.Posts;
using FriendCore.Domain.Users;
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

    public Task<Post?> GetByIdForModerationAsync(Ulid id, CancellationToken cancellationToken = default)
    {
        // Tracked + ignores all query filters (block and soft-delete) so moderation can reach
        // any reported post.
        return context.Posts
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(post => post.Id == id, cancellationToken);
    }

    public void StageSoftDelete(Post post)
    {
        // The SoftDeleteInterceptor converts this Remove into a DeletedAt stamp at SaveChanges
        // time; no save here — the moderation audit write commits it.
        context.Posts.Remove(post);
    }

    public void StageRestore(Post post)
    {
        // Clearing DeletedAt revives a soft-deleted post; no save here — the appeal review commits it.
        post.DeletedAt = null;
    }

    public async Task<IReadOnlyList<Post>> GetByIdsAsync(
        IReadOnlyCollection<Ulid> ids,
        CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
            return [];

        return await context.Posts
            .Where(post => ids.Contains(post.Id))
            .Include(post => post.Media)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
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

    public async Task StageSoftDeleteCommentsForPostAsync(Ulid postId, CancellationToken cancellationToken = default)
    {
        // Named ignore drops the Blocks filter only: a cascade must reach comments by users
        // the deleter has blocked, or their rows would survive the post. The soft-delete
        // filter stays on — already-deleted comments keep their original DeletedAt.
        var comments = await context.Comments
            .IgnoreQueryFilters([AppDbContext.BlocksFilter])
            .Where(comment => comment.PostId == postId)
            .ToListAsync(cancellationToken);

        // Remove converts to a DeletedAt stamp via the SoftDeleteInterceptor at SaveChanges;
        // no save here — the caller's unit of work commits it with the post.
        foreach (var comment in comments)
            context.Comments.Remove(comment);
    }

    public async Task StageRemoveLikesForPostAsync(Ulid postId, CancellationToken cancellationToken = default)
    {
        // Likes carry no DeletedAt and no query filters — a hard remove is the only delete
        // they have. No save here; the caller's unit of work commits it with the post.
        var likes = await context.Likes
            .Where(like => like.PostId == postId)
            .ToListAsync(cancellationToken);

        context.Likes.RemoveRange(likes);
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
        ContentPreference contentPreference,
        (DateTimeOffset CreatedAt, Ulid Id)? cursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var query = context.Posts
            .Where(post => context.Follows.Any(follow =>
                follow.FollowerId == userId && follow.FollowedId == post.AuthorId))
            .VisibleTo(context, userId, keywordFilters, contentPreference);

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

    public async Task<IReadOnlyList<Ulid>> GetLikerIdsAsync(
        Ulid postId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        return await context.Likes
            .Where(like => like.PostId == postId)
            .OrderByDescending(like => like.CreatedAt)
            .ThenByDescending(like => like.UserId)
            .Take(limit)
            .Select(like => like.UserId)
            .ToListAsync(cancellationToken);
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

    public async Task UpdateCommentAsync(Comment comment, CancellationToken cancellationToken = default)
    {
        context.Comments.Update(comment);
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