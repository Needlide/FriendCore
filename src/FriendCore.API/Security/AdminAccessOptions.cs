namespace FriendCore.API.Security;

/// <summary>
///     Trusted sources for the /api/v1/admin surface (§10.3). When both lists are empty the
///     IP restriction is OFF (role-based auth still applies) — this is the dev/test default, and
///     production should populate at least one. Requests to admin routes from any other IP get a
///     404, so the admin surface is invisible to unknown clients.
/// </summary>
public sealed class AdminAccessOptions
{
    public const string SectionName = "AdminAccess";

    // Exact IPs (e.g. moderator office / VPN egress).
    public string[] AllowedIps { get; set; } = [];

    // CIDR ranges, e.g. "203.0.113.0/24".
    public string[] AllowedNetworks { get; set; } = [];
}
