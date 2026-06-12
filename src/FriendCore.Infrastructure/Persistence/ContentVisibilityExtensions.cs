using FriendCore.Domain.Posts;
using Microsoft.EntityFrameworkCore;

namespace FriendCore.Infrastructure.Persistence;

/// <summary>
///     The build order's "IContentVisibilityService" concept, realised as query composition: the
///     mute and keyword rules must end up inside the SQL (they shape pagination and ranking), so
///     they compose onto the query rather than living behind an injected service. Blocks are
///     deliberately NOT here — they are global query filters (§8.2) and apply to every query
///     automatically. Used by the home feed and both discovery queries; deliberate navigation
///     (direct post access, profile listings) stays unfiltered.
/// </summary>
internal static class ContentVisibilityExtensions
{
    public static IQueryable<Post> VisibleTo(
        this IQueryable<Post> posts,
        AppDbContext context,
        Ulid viewerId,
        IReadOnlyList<string> keywordFilters)
    {
        // One-way mute (§13.6): hides the muted user's posts from this viewer only.
        posts = posts.Where(post =>
            !context.Mutes.Any(mute => mute.MuterId == viewerId && mute.MutedId == post.AuthorId));

        // Keyword filters (§13.6): case-insensitive substring, applied before any page/limit cut.
        // Media-only posts (null content) can never match.
        if (keywordFilters.Count > 0)
            posts = posts.Where(post =>
                post.Content == null
                || !keywordFilters.Any(keyword => EF.Functions.ILike(post.Content!, "%" + keyword + "%")));

        return posts;
    }
}
