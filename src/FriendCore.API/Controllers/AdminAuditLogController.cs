using FriendCore.API.Authorization;
using FriendCore.API.Extensions;
using FriendCore.Application.Moderation.Queries.GetAuditLog;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FriendCore.API.Controllers;

/// <summary>§13.5 moderator audit log — every action by every moderator. Moderator and up.</summary>
[ApiController]
[Route("api/v1/admin/audit-log")]
[Authorize(Policy = AuthorizationPolicies.Moderation)]
[EnableRateLimiting(RateLimitingExtensions.ApiPolicy)]
public sealed class AdminAuditLogController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetLog(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetAuditLogQuery(), cancellationToken);
        return result.ToActionResult();
    }
}
