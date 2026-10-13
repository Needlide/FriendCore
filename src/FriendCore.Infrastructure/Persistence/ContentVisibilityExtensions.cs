using FriendCore.Domain.Posts;
using FriendCore.Domain.Users;
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
        IReadOnlyList<string> keywordFilters,
        ContentPreference contentPreference)
    {
        // One-way mute (§13.6): hides the muted user's posts from this viewer only.
        posts = posts.Where(post =>
            !context.Mutes.Any(mute => mute.MuterId == viewerId && mute.MutedId == post.AuthorId));

        // Keyword filters (§13.6): case-insensitive substring, applied before any page/limit cut.
        // Media-only posts (null content) can never match.
        //
        // Contains rather than a LIKE-ish helper, and one Where per keyword rather than an Any over
        // the list: EF compiles Contains to `lower(content) LIKE @param` with the pattern bound and
        // its LIKE metacharacters escaped, so a keyword containing % or _ is matched literally.
        //
        // This replaced EF.Functions.ILike, which was a real bug and not a style point. It compiles
        // to `content ILIKE @term ESCAPE ''` — an EMPTY escape character — so "_" stayed a
        // single-character wildcard and the keyword "snake_case" silently hid "snakeXcase".
        // ProviderQuirkGuardTests fails the build if that helper comes back.
        //
        // Looping is equivalent to the Any it replaces: "no keyword matches" is the AND of each
        // "this keyword does not match", and a closure over the loop variable would otherwise be
        // captured incorrectly anyway.
        foreach (var keyword in keywordFilters)
        {
            var needle = keyword.ToLowerInvariant();
            posts = posts.Where(post => post.Content == null || !post.Content.ToLower().Contains(needle));
        }

        // §9.1: explicit/sensitive content filtered by the viewer's ContentPreference. Enums are
        // stored as strings, where <= would compare alphabetically — hence an explicit IN-list.
        var allowed = AllowedSensitivities(contentPreference);
        if (allowed.Count < 3)
            posts = posts.Where(post => allowed.Contains(post.Sensitivity));

        return posts;
    }

    private static List<ContentSensitivity> AllowedSensitivities(ContentPreference preference) =>
        preference switch
        {
            ContentPreference.General => [ContentSensitivity.General],
            ContentPreference.Sensitive => [ContentSensitivity.General, ContentSensitivity.Sensitive],
            _ => [ContentSensitivity.General, ContentSensitivity.Sensitive, ContentSensitivity.Explicit]
        };
}
