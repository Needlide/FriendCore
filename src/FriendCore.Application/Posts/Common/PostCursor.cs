using System.Globalization;

namespace FriendCore.Application.Posts.Common;

// Opaque cursor for post pagination, encoded as the UTC tick count of a post's CreatedAt.
internal static class PostCursor
{
    public static string Encode(DateTimeOffset createdAt) =>
        createdAt.UtcTicks.ToString(CultureInfo.InvariantCulture);

    public static DateTimeOffset? Decode(string? cursor) =>
        long.TryParse(cursor, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks)
            ? new DateTimeOffset(ticks, TimeSpan.Zero)
            : null;
}
