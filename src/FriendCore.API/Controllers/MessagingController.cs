using FriendCore.API.Contracts.Messaging;
using FriendCore.API.Extensions;
using FriendCore.Application.Messaging.Commands.MarkConversationRead;
using FriendCore.Application.Messaging.Commands.SendMessage;
using FriendCore.Application.Messaging.Commands.StartConversation;
using FriendCore.Application.Messaging.Commands.StartSupportConversation;
using FriendCore.Application.Messaging.Queries.GetConversations;
using FriendCore.Application.Messaging.Queries.GetMessages;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FriendCore.API.Controllers;

[ApiController]
[Route("api/v1/conversations")]
[EnableRateLimiting(RateLimitingExtensions.MessagingPolicy)]
public sealed class MessagingController(ISender sender) : ControllerBase
{
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetConversations(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetConversationsQuery(), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> StartConversation(
        [FromBody] StartConversationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new StartConversationCommand(request.Username), cancellationToken);
        return result.ToCreatedResult();
    }

    /// <summary>
    ///     Find-or-create the caller's 1:1 conversation with the support inbox. Informal channel
    ///     (thanks, bug reports, quick questions) — not a substitute for the report/appeal system.
    /// </summary>
    [HttpPost("support")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> StartSupportConversation(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new StartSupportConversationCommand(), cancellationToken);
        return result.ToCreatedResult();
    }

    [HttpGet("{id}/messages")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMessages(
        string id,
        [FromQuery] string? cursor,
        CancellationToken cancellationToken)
    {
        if (!Ulid.TryParse(id, out var conversationId))
            return NotFound();

        var result = await sender.Send(new GetMessagesQuery(conversationId, cursor), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("{id}/messages")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SendMessage(
        string id,
        [FromBody] SendMessageRequest request,
        CancellationToken cancellationToken)
    {
        if (!Ulid.TryParse(id, out var conversationId))
            return NotFound();

        // A supplied-but-unparseable media id is a client bug, not an absent attachment.
        // Silently coercing it to null would turn a request for an image into a
        // successful TEXT-ONLY message — the caller would see 201 and never know the
        // picture was dropped.
        Ulid? mediaId = null;
        if (request.MediaId is { Length: > 0 })
        {
            if (!Ulid.TryParse(request.MediaId, out var parsed))
                return BadRequest(new
                {
                    errors = new Dictionary<string, string[]>
                    {
                        [nameof(request.MediaId)] = ["Media id is not a valid id."]
                    }
                });
            mediaId = parsed;
        }

        var result = await sender.Send(
            new SendMessageCommand(conversationId, request.Content, mediaId), cancellationToken);
        return result.ToCreatedResult();
    }

    [HttpPost("{id}/read")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkRead(string id, CancellationToken cancellationToken)
    {
        if (!Ulid.TryParse(id, out var conversationId))
            return NotFound();

        var result = await sender.Send(new MarkConversationReadCommand(conversationId), cancellationToken);
        return result.IsError ? ErrorOrExtensions.FromErrors(result.Errors) : NoContent();
    }
}
