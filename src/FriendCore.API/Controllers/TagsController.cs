using FriendCore.API.Authorization;
using FriendCore.API.Contracts.Tags;
using FriendCore.API.Extensions;
using FriendCore.Application.Tags.Commands.ArchiveStaleTags;
using FriendCore.Application.Tags.Commands.CreateOrGetTag;
using FriendCore.Application.Tags.Commands.ModerateTag;
using FriendCore.Application.Tags.Queries.GetTagAutocomplete;
using FriendCore.Application.Tags.Queries.GetTagDetails;
using FriendCore.Application.Tags.Queries.GetTagModerationQueue;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FriendCore.API.Controllers;

[ApiController]
[Route("api/v1/tags")]
[EnableRateLimiting(RateLimitingExtensions.ApiPolicy)]
public sealed class TagsController(ISender sender) : ControllerBase
{
    [HttpGet("autocomplete")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Autocomplete([FromQuery] string query, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetTagAutocompleteQuery(query ?? string.Empty), cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("{slug}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetails(string slug, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetTagDetailsQuery(slug), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateOrGet([FromBody] CreateTagRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateOrGetTagCommand(request.Name), cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("moderation/queue")]
    [Authorize(Policy = AuthorizationPolicies.TagModeration)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ModerationQueue(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetTagModerationQueueQuery(), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("{id}/moderation")]
    [Authorize(Policy = AuthorizationPolicies.TagModeration)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Moderate(
        string id,
        [FromBody] ModerateTagRequest request,
        CancellationToken cancellationToken)
    {
        if (!Ulid.TryParse(id, out var tagId))
            return NotFound();

        var result = await sender.Send(new ModerateTagCommand(tagId, request.Action), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("moderation/auto-archive")]
    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AutoArchiveStaleTags(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ArchiveStaleTagsCommand(), cancellationToken);
        return result.ToActionResult();
    }
}