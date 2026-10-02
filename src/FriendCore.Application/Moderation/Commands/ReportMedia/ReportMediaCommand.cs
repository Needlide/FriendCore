using ErrorOr;
using FriendCore.Application.Moderation.Common;
using MediatR;

namespace FriendCore.Application.Moderation.Commands.ReportMedia;

/// <summary>
/// Files an image into the moderation queue under §13.4's categories.
///
/// This replaces the old shortcut: POST /media/{id}/report-safety took
/// {flashing, intenseMotion} and called media.FlagSafety directly, never touching
/// <c>IReportRepository</c>. The media was flagged but no Report row existed, so no moderator
/// queue or metric ever saw it — §7.4's layer 2 was switched off in practice. Filing a real
/// Report is what makes the report visible, deduped, resolvable and auditable.
/// </summary>
public sealed record ReportMediaCommand(Ulid MediaId, ReportCategoryInput Category, string? Note)
    : IRequest<ErrorOr<Success>>;
