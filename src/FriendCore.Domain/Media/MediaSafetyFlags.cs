using System.Collections.ObjectModel;

namespace FriendCore.Domain.Media;

/// <summary>
///     Creator self-declared and moderator-applied safety flags on a media item, stored as a
///     single JSONB dictionary: <c>{ "flashing": true, "arachnophobia": false, ... }</c>.
///
///     One column forever — adding a new hazard (arachnophobia, explicit, strobe, ...) needs
///     zero schema changes and zero new API parameters. The dictionary is additive: flags are
///     only ever set to <c>true</c>, never cleared, so a moderator's decision outranks a later
///     user report just as before.
/// </summary>
public sealed record MediaSafetyFlags(IReadOnlyDictionary<string, bool> Flags)
{
    public static MediaSafetyFlags Empty { get; } = new(new Dictionary<string, bool>());

    /// <summary>
    ///     Builds a flag set from name/value pairs, mainly for callers that used to pass one bool
    ///     per hazard. Names are the wire keys ("flashing", "intenseMotion", ...).
    /// </summary>
    public static MediaSafetyFlags From(params (string Name, bool Value)[] flags)
    {
        var copy = new Dictionary<string, bool>(flags.Length);
        foreach (var (name, value) in flags)
            copy[name] = value;
        return new MediaSafetyFlags(copy);
    }

    /// <summary>True when the named flag is set.</summary>
    public bool IsSet(string name) => Flags.TryGetValue(name, out var value) && value;

    /// <summary>
    ///     Additive merge: every <c>true</c> flag in <paramref name="other" /> is set on this
    ///     record, existing flags are never cleared. Used by <see cref="MediaItem.FlagSafety" />
    ///     so a moderator's decision stays authoritative.
    /// </summary>
    public MediaSafetyFlags Merge(MediaSafetyFlags other)
    {
        var copy = new Dictionary<string, bool>(Flags);
        foreach (var (name, value) in other.Flags)
        {
            if (value) copy[name] = true;
        }
        return new MediaSafetyFlags(new ReadOnlyDictionary<string, bool>(copy));
    }
}
