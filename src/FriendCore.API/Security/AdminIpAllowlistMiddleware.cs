using System.Net;
using Microsoft.Extensions.Options;

namespace FriendCore.API.Security;

/// <summary>
///     §10.3 / §15.1: requests to /api/v1/admin from non-allowlisted IPs receive 404 (not 403),
///     hiding the admin surface from unknown clients. Relies on the real client IP, so it must run
///     after UseForwardedHeaders. No-op when no allowlist is configured (dev/test).
/// </summary>
public sealed class AdminIpAllowlistMiddleware
{
    private const string AdminPathPrefix = "/api/v1/admin";

    private readonly RequestDelegate _next;
    private readonly ILogger<AdminIpAllowlistMiddleware> _logger;
    private readonly List<IPAddress> _allowedIps;
    private readonly List<IPNetwork> _allowedNetworks;
    private readonly bool _restrict;

    public AdminIpAllowlistMiddleware(
        RequestDelegate next,
        IOptions<AdminAccessOptions> options,
        ILogger<AdminIpAllowlistMiddleware> logger)
    {
        _next = next;
        _logger = logger;

        var configured = options.Value;
        _allowedIps = configured.AllowedIps
            .Select(value => IPAddress.TryParse(value.Trim(), out var ip) ? ip : null)
            .Where(ip => ip is not null)
            .Select(ip => ip!)
            .ToList();
        _allowedNetworks = configured.AllowedNetworks
            .Select(value => IPNetwork.TryParse(value.Trim(), out var network) ? network : (IPNetwork?)null)
            .Where(network => network is not null)
            .Select(network => network!.Value)
            .ToList();

        _restrict = _allowedIps.Count > 0 || _allowedNetworks.Count > 0;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (_restrict && context.Request.Path.StartsWithSegments(AdminPathPrefix))
        {
            var clientIp = context.Connection.RemoteIpAddress;
            if (clientIp is null || !IsAllowed(clientIp))
            {
                _logger.LogWarning("Blocked admin request to {Path} from {Ip}", context.Request.Path, clientIp);
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }
        }

        await _next(context);
    }

    private bool IsAllowed(IPAddress clientIp)
    {
        // Normalise IPv4-mapped IPv6 (e.g. ::ffff:203.0.113.7) so a v4 allowlist still matches.
        var ip = clientIp.IsIPv4MappedToIPv6 ? clientIp.MapToIPv4() : clientIp;

        return _allowedIps.Any(allowed => allowed.Equals(ip))
               || _allowedNetworks.Any(network => network.Contains(ip));
    }
}
