using FriendCore.Domain.Moderation;

namespace FriendCore.Application.Moderation.Common;

// Application-level mirror of Domain ReportCategory so API contracts never bind Domain types
// (§2.2 layering, enforced by the architecture suite). Member names are the wire format.
public enum ReportCategoryInput
{
    Spam,
    Harassment,
    NudityOrSexual,
    ViolenceOrDangerous,
    PrivateInformation,
    Copyright,
    InvolvesMinor,
    FlashingLights,
    Other
}

internal static class ReportCategoryMapping
{
    // Out-of-range values are rejected by the validator's IsInEnum before this runs.
    public static ReportCategory ToDomain(this ReportCategoryInput input) =>
        input switch
        {
            ReportCategoryInput.Spam => ReportCategory.Spam,
            ReportCategoryInput.Harassment => ReportCategory.Harassment,
            ReportCategoryInput.NudityOrSexual => ReportCategory.NudityOrSexual,
            ReportCategoryInput.ViolenceOrDangerous => ReportCategory.ViolenceOrDangerous,
            ReportCategoryInput.PrivateInformation => ReportCategory.PrivateInformation,
            ReportCategoryInput.Copyright => ReportCategory.Copyright,
            ReportCategoryInput.InvolvesMinor => ReportCategory.InvolvesMinor,
            ReportCategoryInput.FlashingLights => ReportCategory.FlashingLights,
            ReportCategoryInput.Other => ReportCategory.Other,
            _ => throw new ArgumentOutOfRangeException(nameof(input), input, null)
        };
}
