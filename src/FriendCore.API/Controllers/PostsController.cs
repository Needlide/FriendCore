using FriendCore.API.Contracts.Posts;
using FriendCore.API.Extensions;
using FriendCore.Application.Posts.Commands.CreatePost;
using FriendCore.Application.Posts.Commands.DeletePost;
using FriendCore.Application.Posts.Queries.GetPost;
using FriendCore.Application.Posts.Queries.GetUserPosts;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FriendCore.API.Controllers;

[ApiController]
[Route("api/v1/posts")]
[EnableRateLimiting(RateLimitingExtensions.ApiPolicy)]
public sealed class PostsController(ISender sender) : ControllerBase
{
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create([FromBody] CreatePostRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreatePostCommand(request.Content, request.Sensitivity), cancellationToken);
        return result.ToCreatedResult();
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string id, CancellationToken cancellationToken)
    {
        if (!Ulid.TryParse(id, out var postId))
            return NotFound();

        var result = await sender.Send(new GetPostQuery(postId), cancellationToken);
        return result.ToActionResult();
    }

    [HttpDelete("{id}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        if (!Ulid.TryParse(id, out var postId))
            return NotFound();

        var result = await sender.Send(new DeletePostCommand(postId), cancellationToken);
        return result.IsError ? ErrorOrExtensions.FromErrors(result.Errors) : NoContent();
    }

    [HttpGet("/api/v1/users/{username}/posts")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserPosts(
        string username,
        [FromQuery] string? cursor,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetUserPostsQuery(username, cursor), cancellationToken);
        return result.ToActionResult();
    }
}
