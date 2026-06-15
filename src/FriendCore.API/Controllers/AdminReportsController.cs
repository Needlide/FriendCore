using FriendCore.API.Authorization;
using FriendCore.API.Extensions;
using FriendCore.Application.Moderation.Queries.GetReport;
using FriendCore.Application.Moderation.Queries.GetReportQueue;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FriendCore.API.Controllers;

/// <summary>
///     §13.5 report queue + individual report view. Under /api/v1/admin (§10.3), gated to
///     Moderator and up. (Trusted-IP restriction is deployment hardening, deferred.)
/// </summary>
[ApiController]
[Route("api/v1/admin/reports")]
[Authorize(Policy = AuthorizationPolicies.Moderation)]
[EnableRateLimiting(RateLimitingExtensions.ApiPolicy)]
public sealed class AdminReportsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetQueue(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetReportQueueQuery(), cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReport(string id, CancellationToken cancellationToken)
    {
        if (!Ulid.TryParse(id, out var reportId))
            return NotFound();

        var result = await sender.Send(new GetReportQuery(reportId), cancellationToken);
        return result.ToActionResult();
    }
}
