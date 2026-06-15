namespace FriendCore.API.Authorization;

public static class AuthorizationPolicies
{
    public const string TagModeration = "TagModeration";
    public const string TagMerge = "TagMerge";
    public const string Admin = "Admin";

    // Moderator and up — gates the /api/v1/admin moderation endpoints (reports, audit, appeals).
    public const string Moderation = "Moderation";
}
