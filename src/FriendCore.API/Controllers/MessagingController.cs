using FriendCore.API.Contracts.Messaging;
using FriendCore.API.Contracts.Moderation;
using FriendCore.API.Extensions;
using FriendCore.Application.Messaging.Commands.DeleteMessage;
using FriendCore.Application.Messaging.Commands.EditMessage;
using FriendCore.Application.Messaging.Commands.MarkConversationRead;
using FriendCore.Application.Messaging.Commands.SendMessage;
using FriendCore.Application.Messaging.Commands.StartConversation;
using FriendCore.Application.Messaging.Commands.StartSupportConversation;
using FriendCore.Application.Messaging.Queries.GetConversations;
using FriendCore.Application.Messaging.Queries.GetMessages;
using FriendCore.Application.Moderation.Commands.ReportMessage;
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

    /// <summary>
    /// Rewrites the text of one of your own messages. PATCH-shaped and returns the full updated
    /// message, so the client renders the server's wording (post-trim) rather than echoing its own
    /// draft. The recipient is pushed the new state live.
    /// </summary>
    [HttpPatch("{id}/messages/{messageId}")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EditMessage(
        string id,
        string messageId,
        [FromBody] EditMessageRequest request,
        CancellationToken cancellationToken)
    {
        // A malformed id on either segment is a 404, never a 400 — the same rule every other
        // messaging action uses, and it keeps probing from learning which ids are real.
        if (!Ulid.TryParse(id, out var conversationId) || !Ulid.TryParse(messageId, out var parsedMessageId))
            return NotFound();

        var result = await sender.Send(
            new EditMessageCommand(conversationId, parsedMessageId, request.Content), cancellationToken);
        return result.ToActionResult();
    }

    /// <summary>
    /// Soft-deletes one of your own messages: the text is scrubbed and the row drops out of every
    /// query, for you and the recipient alike. There is no tombstone to render — a deleted message
    /// is simply gone.
    /// </summary>
    [HttpDelete("{id}/messages/{messageId}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteMessage(
        string id,
        string messageId,
        CancellationToken cancellationToken)
    {
        if (!Ulid.TryParse(id, out var conversationId) || !Ulid.TryParse(messageId, out var parsedMessageId))
            return NotFound();

        var result = await sender.Send(
            new DeleteMessageCommand(conversationId, parsedMessageId), cancellationToken);
        return result.IsError ? ErrorOrExtensions.FromErrors(result.Errors) : NoContent();
    }

    /// <summary>
    /// Reports one message to the moderation queue. Scoped to the conversation so a guessed
    /// message id is a 404; the reporter's own message is refused. Same 9 categories and same
    /// idempotency as post and user reports — filing twice is a silent no-op, not an error.
    /// </summary>
    [HttpPost("{id}/messages/{messageId}/report")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReportMessage(
        string id,
        string messageId,
        [FromBody] ReportRequest request,
        CancellationToken cancellationToken)
    {
        if (!Ulid.TryParse(id, out var conversationId) || !Ulid.TryParse(messageId, out var parsedMessageId))
            return NotFound();

        var result = await sender.Send(
            new ReportMessageCommand(conversationId, parsedMessageId, request.Category, request.Note),
            cancellationToken);
        return result.IsError ? ErrorOrExtensions.FromErrors(result.Errors) : NoContent();
    }
}
