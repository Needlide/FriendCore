using FriendCore.API.Contracts.Media;
using FriendCore.API.Extensions;
using FriendCore.Application.Media.Commands.DeleteMedia;
using FriendCore.Application.Media.Commands.ReportMediaSafety;
using FriendCore.Application.Media.Commands.UploadMedia;
using FriendCore.Application.Media.Queries.GetMediaStatus;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FriendCore.API.Controllers;

[ApiController]
[Route("api/v1/media")]
[EnableRateLimiting(RateLimitingExtensions.ApiPolicy)]
public sealed class MediaController(ISender sender) : ControllerBase
{
    [HttpPost]
    [Authorize]
    // App-level validation rejects > 20MB with a clean 400; this just caps the request pipe with
    // headroom for multipart framing.
    [RequestSizeLimit(22 * 1024 * 1024)]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Upload(
        [FromForm] UploadMediaRequest request,
        CancellationToken cancellationToken)
    {
        // A missing file part flows through as an empty payload → Media.FileRequired from the
        // validator (error shapes stay consistent with every other endpoint).
        await using var content = request.File?.OpenReadStream() ?? Stream.Null;

        var result = await sender.Send(
            new UploadMediaCommand(
                content,
                request.File?.Length ?? 0,
                request.File?.FileName ?? string.Empty,
                request.File?.ContentType ?? string.Empty,
                request.HasFlashingWarning,
                request.HasIntenseMotionWarning),
            cancellationToken);

        return result.ToCreatedResult();
    }

    [HttpGet("{id}/status")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStatus(string id, CancellationToken cancellationToken)
    {
        if (!Ulid.TryParse(id, out var mediaId))
            return NotFound();

        var result = await sender.Send(new GetMediaStatusQuery(mediaId), cancellationToken);
        return result.ToActionResult();
    }

    [HttpDelete("{id}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        if (!Ulid.TryParse(id, out var mediaId))
            return NotFound();

        var result = await sender.Send(new DeleteMediaCommand(mediaId), cancellationToken);
        return result.IsError ? ErrorOrExtensions.FromErrors(result.Errors) : NoContent();
    }

    [HttpPost("{id}/report-safety")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReportSafety(
        string id,
        [FromBody] ReportMediaSafetyRequest request,
        CancellationToken cancellationToken)
    {
        if (!Ulid.TryParse(id, out var mediaId))
            return NotFound();

        var result = await sender.Send(
            new ReportMediaSafetyCommand(mediaId, request.Flashing, request.IntenseMotion),
            cancellationToken);
        return result.IsError ? ErrorOrExtensions.FromErrors(result.Errors) : NoContent();
    }
}
