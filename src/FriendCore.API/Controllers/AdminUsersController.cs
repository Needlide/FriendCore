using FriendCore.API.Authorization;
using FriendCore.API.Contracts.Moderation;
using FriendCore.API.Extensions;
using FriendCore.Application.Moderation.Commands.ActOnUser;
using FriendCore.Application.Moderation.Queries.GetUserAccount;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FriendCore.API.Controllers;

/// <summary>§13.5 user account view + enforcement actions. Under /api/v1/admin, Moderator and up.</summary>
[ApiController]
[Route("api/v1/admin/users")]
[Authorize(Policy = AuthorizationPolicies.Moderation)]
[EnableRateLimiting(RateLimitingExtensions.ApiPolicy)]
public sealed class AdminUsersController(ISender sender) : ControllerBase
{
    [HttpGet("{username}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAccount(string username, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetUserAccountQuery(username), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("{username}/action")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Act(
        string username,
        [FromBody] ActOnUserRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ActOnUserCommand(username, request.Action, request.Reason, request.SuspensionDays),
            cancellationToken);
        return result.IsError ? ErrorOrExtensions.FromErrors(result.Errors) : NoContent();
    }
}
