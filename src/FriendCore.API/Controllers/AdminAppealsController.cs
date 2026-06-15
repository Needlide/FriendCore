using FriendCore.API.Authorization;
using FriendCore.API.Contracts.Moderation;
using FriendCore.API.Extensions;
using FriendCore.Application.Moderation.Commands.ReviewAppeal;
using FriendCore.Application.Moderation.Queries.GetAppealQueue;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FriendCore.API.Controllers;

/// <summary>§13.3/§13.5 appeals queue + review. Separate from the report queue. Moderator and up.</summary>
[ApiController]
[Route("api/v1/admin/appeals")]
[Authorize(Policy = AuthorizationPolicies.Moderation)]
[EnableRateLimiting(RateLimitingExtensions.ApiPolicy)]
public sealed class AdminAppealsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetQueue(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetAppealQueueQuery(), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("{id}/review")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Review(
        string id,
        [FromBody] ReviewAppealRequest request,
        CancellationToken cancellationToken)
    {
        if (!Ulid.TryParse(id, out var appealId))
            return NotFound();

        var result = await sender.Send(
            new ReviewAppealCommand(appealId, request.Outcome, request.Note), cancellationToken);
        return result.IsError ? ErrorOrExtensions.FromErrors(result.Errors) : NoContent();
    }
}
