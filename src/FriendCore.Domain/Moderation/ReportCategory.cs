namespace FriendCore.Domain.Moderation;

// §13.4 user-facing report categories.
public enum ReportCategory
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
