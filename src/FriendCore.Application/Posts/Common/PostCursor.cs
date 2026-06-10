using System.Globalization;

namespace FriendCore.Application.Posts.Common;

// Opaque keyset cursor for post/comment pagination. Encodes the last row's (CreatedAt, Id) so
// rows that share a CreatedAt are still ordered deterministically by their (time-sortable) Ulid,
// eliminating the skip/duplicate a CreatedAt-only cursor suffers at a tied-timestamp boundary.
internal static class PostCursor
{
    public static string Encode(DateTimeOffset createdAt, Ulid id)
    {
        return $"{createdAt.UtcTicks.ToString(CultureInfo.InvariantCulture)}_{id}";
    }

    public static (DateTimeOffset CreatedAt, Ulid Id)? Decode(string? cursor)
    {
        if (string.IsNullOrEmpty(cursor))
            return null;

        var separator = cursor.IndexOf('_');
        if (separator <= 0)
            return null;

        if (!long.TryParse(
                cursor.AsSpan(0, separator),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var ticks))
            return null;

        if (!Ulid.TryParse(cursor.AsSpan(separator + 1), out var id))
            return null;

        return (new DateTimeOffset(ticks, TimeSpan.Zero), id);
    }
}