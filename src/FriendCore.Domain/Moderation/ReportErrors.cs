using ErrorOr;

namespace FriendCore.Domain.Moderation;

public static class ReportErrors
{
    public static Error NotFound =>
        Error.NotFound("Report.NotFound", "Report was not found.");

    public static Error CannotReportSelf =>
        Error.Validation("Report.CannotReportSelf", "You cannot report yourself.");

    public static Error NoteTooLong =>
        Error.Validation("Report.NoteTooLong",
            $"Report note must not exceed {Report.MaxNoteLength} characters.");

    public static Error AlreadyResolved =>
        Error.Conflict("Report.AlreadyResolved", "This report has already been resolved.");
}
