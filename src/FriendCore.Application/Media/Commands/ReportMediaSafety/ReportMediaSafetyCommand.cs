using ErrorOr;
using MediatR;

namespace FriendCore.Application.Media.Commands.ReportMediaSafety;

/// <summary>
///     Spec §7.4 detection layer 2: any authenticated user can report media as containing flashing
///     or intense-motion content.
/// </summary>
public sealed record ReportMediaSafetyCommand(
    Ulid MediaId,
    bool Flashing,
    bool IntenseMotion) : IRequest<ErrorOr<Success>>;
