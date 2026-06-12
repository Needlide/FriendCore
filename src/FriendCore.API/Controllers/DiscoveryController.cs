using FriendCore.API.Extensions;
using FriendCore.Application.Discovery.Queries.GetDiscoveryPeople;
using FriendCore.Application.Discovery.Queries.GetDiscoveryPosts;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FriendCore.API.Controllers;

[ApiController]
[Route("api/v1/feed/discovery")]
[EnableRateLimiting(RateLimitingExtensions.ApiPolicy)]
public sealed class DiscoveryController(ISender sender) : ControllerBase
{
    [HttpGet("people")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetPeople(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetDiscoveryPeopleQuery(), cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("posts")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetPosts(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetDiscoveryPostsQuery(), cancellationToken);
        return result.ToActionResult();
    }
}
