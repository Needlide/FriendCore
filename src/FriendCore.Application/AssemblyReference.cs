using System.Reflection;

namespace FriendCore.Application;

/// <summary>
///     Marker type for assembly scanning (MediatR, FluentValidation, architecture tests).
/// </summary>
public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}