using ErrorOr;

namespace FriendCore.Domain.Common;

public static class CommonErrors
{
    public static readonly Error Unauthorized =
        Error.Unauthorized("Common.Unauthorized", "Authentication is required.");

    public static readonly Error Forbidden =
        Error.Forbidden("Common.Forbidden", "You do not have permission to perform this action.");

    public static readonly Error Unexpected =
        Error.Unexpected("Common.Unexpected", "An unexpected error occurred.");
}