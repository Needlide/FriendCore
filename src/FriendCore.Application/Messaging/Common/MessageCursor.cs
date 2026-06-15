using System.Globalization;

namespace FriendCore.Application.Messaging.Common;

// Opaque keyset cursor for message pagination — same (CreatedAt, Ulid) scheme as PostCursor.
// (The three keyset cursors in the app could be unified into one shared helper later.)
internal static class MessageCursor
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
