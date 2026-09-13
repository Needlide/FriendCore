using FriendCore.API.Extensions;
using FriendCore.Application.Posts.Queries.GetHomeFeed;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FriendCore.API.Controllers;

[ApiController]
[Route("api/v1/feed")]
[EnableRateLimiting(RateLimitingExtensions.ApiPolicy)]
public sealed class FeedController(ISender sender) : ControllerBase
{
    [HttpGet("home")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetHomeFeed([FromQuery] string? cursor, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetHomeFeedQuery(cursor), cancellationToken);
        return result.ToActionResult();
    }
}
