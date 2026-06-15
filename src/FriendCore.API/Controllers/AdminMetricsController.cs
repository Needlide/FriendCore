using FriendCore.API.Authorization;
using FriendCore.API.Extensions;
using FriendCore.Application.Moderation.Queries.GetModerationMetrics;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FriendCore.API.Controllers;

/// <summary>§13.5 metrics dashboard: volume, response times, overturn rate. Moderator and up.</summary>
[ApiController]
[Route("api/v1/admin/metrics")]
[Authorize(Policy = AuthorizationPolicies.Moderation)]
[EnableRateLimiting(RateLimitingExtensions.ApiPolicy)]
public sealed class AdminMetricsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMetrics(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetModerationMetricsQuery(), cancellationToken);
        return result.ToActionResult();
    }
}
