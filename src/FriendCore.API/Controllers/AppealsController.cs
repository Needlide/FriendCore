using FriendCore.API.Contracts.Moderation;
using FriendCore.API.Extensions;
using FriendCore.Application.Moderation.Commands.SubmitAppeal;
using FriendCore.Application.Moderation.Queries.GetMyModerationActions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FriendCore.API.Controllers;

/// <summary>User-facing appeals (§13.3): see actions taken against you, and appeal one.</summary>
[ApiController]
[Route("api/v1")]
[Authorize]
[EnableRateLimiting(RateLimitingExtensions.ApiPolicy)]
public sealed class AppealsController(ISender sender) : ControllerBase
{
    [HttpGet("users/me/moderation-actions")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MyActions(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetMyModerationActionsQuery(), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("appeals")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Submit(
        [FromBody] SubmitAppealRequest request,
        CancellationToken cancellationToken)
    {
        if (!Ulid.TryParse(request.AuditLogId, out var auditLogId))
            return NotFound();

        var result = await sender.Send(new SubmitAppealCommand(auditLogId, request.Reason), cancellationToken);
        return result.IsError ? ErrorOrExtensions.FromErrors(result.Errors) : NoContent();
    }
}
