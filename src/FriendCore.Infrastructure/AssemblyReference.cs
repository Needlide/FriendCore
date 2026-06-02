using System.Reflection;

namespace FriendCore.Infrastructure;

/// <summary>
///     Marker type for assembly scanning (EF configurations, architecture tests).
/// </summary>
public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}