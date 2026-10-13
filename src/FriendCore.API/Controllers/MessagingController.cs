using FriendCore.API.Contracts.Messaging;
using FriendCore.API.Contracts.Moderation;
using FriendCore.API.Extensions;
using FriendCore.Application.Messaging.Commands.DeleteMessage;
using FriendCore.Application.Messaging.Commands.HideMessage;
using FriendCore.Application.Common.Models;
using FriendCore.Application.Messaging.Queries.GetMessagesAround;
using FriendCore.Application.Messaging.Queries.SearchMessages;
using FriendCore.Application.Messaging.Commands.EditMessage;
using FriendCore.Application.Messaging.Commands.MarkConversationRead;
using FriendCore.Application.Messaging.Commands.SendMessage;
using FriendCore.Application.Messaging.Commands.StartConversation;
using FriendCore.Application.Messaging.Commands.StartSupportConversation;
using FriendCore.Application.Messaging.Commands.ToggleMessageReaction;
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

        // Same reasoning as a malformed media id: a supplied-but-unparseable reply target is a
        // client bug, not an absent one, and silently dropping it would send a message that looks
        // like a reply but carries no quote.
        Ulid? replyToId = null;
        if (request.ReplyToMessageId is { Length: > 0 })
        {
            if (!Ulid.TryParse(request.ReplyToMessageId, out var parsedReplyTo))
                return BadRequest(new { errors = new Dictionary<string, string[]> {
                    [nameof(request.ReplyToMessageId)] = ["Reply-to id is not a valid id."] } });
            replyToId = parsedReplyTo;
        }

        var result = await sender.Send(
            new SendMessageCommand(conversationId, request.Content, mediaId, replyToId), cancellationToken);
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
    ///
    /// The body must carry the version the client was looking at. If the message has moved on
    /// since — almost always the same sender saving a reword from another device — the edit is
    /// refused with 409 rather than overwriting it, and the response's bumped version is what
    /// keeps that sender's other devices from 409ing on their next edit.
    /// </summary>
    [HttpPatch("{id}/messages/{messageId}")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
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

        // A missing version is a 400, unlike the ids: this is a client that has not caught up
        // with the contract, not an attempt to probe. Answering without it would silently
        // re-enable last-write-wins for exactly the clients least likely to want that.
        if (request.Version is not { } expectedVersion)
            return BadRequest(new { errors = new Dictionary<string, string[]> {
                [nameof(request.Version)] = ["The version you edited is required."] } });

        var result = await sender.Send(
            new EditMessageCommand(conversationId, parsedMessageId, request.Content, expectedVersion),
            cancellationToken);
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
    /// Finds messages in this conversation containing a term, newest first. Scoped to the caller
    /// exactly like reading the thread: participants only, refused across a block, and messages
    /// the caller has hidden are excluded.
    /// </summary>
    /// <remarks>
    ///     The whole term is matched as one case-insensitive substring — typing a remembered
    ///     phrase finds that phrase, rather than each word separately. At least two characters are
    ///     required, and the result is capped at 50 with no paging: a list long enough to scroll is
    ///     a sign the term is too broad. Text only; attachments and EXIF are not searched.
    /// </remarks>
    [HttpGet("{id}/messages/search")]
    [Authorize]
    [ProducesResponseType(typeof(MessageFeedResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SearchMessages(
        string id,
        [FromQuery] string? q,
        CancellationToken cancellationToken)
    {
        if (!Ulid.TryParse(id, out var conversationId))
            return NotFound();

        var result = await sender.Send(
            new SearchMessagesQuery(conversationId, q), cancellationToken);
        return result.IsError ? ErrorOrExtensions.FromErrors(result.Errors) : Ok(result.Value);
    }

    /// <summary>
    /// Returns a window of the conversation centred on one message, so a search result can be
    /// opened where it actually sits. Pages in BOTH directions, unlike the one-way feed.
    /// </summary>
    /// <remarks>
    ///     Scoped exactly like reading the thread — participants only, refused across a block,
    ///     messages the caller has hidden excluded. 25 either side; cursors continue outward from
    ///     each edge. A message id that is not in this conversation is a 404, not a 400.
    /// </remarks>
    [HttpGet("{id}/messages/{messageId}/around")]
    [Authorize]
    [ProducesResponseType(typeof(MessageWindowResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMessagesAround(
        string id,
        string messageId,
        CancellationToken cancellationToken)
    {
        if (!Ulid.TryParse(id, out var conversationId))
            return NotFound();

        // messageId is deliberately NOT parsed here: the handler turns an unparseable one into the
        // same 404 as a well-formed id that is not in this conversation, so the two are
        // indistinguishable from outside.
        var result = await sender.Send(
            new GetMessagesAroundQuery(conversationId, messageId), cancellationToken);
        return result.IsError ? ErrorOrExtensions.FromErrors(result.Errors) : Ok(result.Value);
    }

    /// <summary>
    /// Removes a message from YOUR view only: the row, its text, and its attachment are all left
    /// exactly as they are, so the other participant still has it. Any image it carried is
    /// recorded as refused between you two, so it cannot come back.
    /// </summary>
    /// <remarks>
    ///     The private counterpart to <see cref="DeleteMessage" />, which removes a message for
    ///     both of you. Deliberately silent — the other participant is not told, because that
    ///     would leak that you acted on it, and the consent refusal does the protective work on
    ///     its own. Hiding twice is a no-op, not an error.
    /// </remarks>
    [HttpPost("{id}/messages/{messageId}/hide")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> HideMessage(
        string id,
        string messageId,
        CancellationToken cancellationToken)
    {
        if (!Ulid.TryParse(id, out var conversationId) || !Ulid.TryParse(messageId, out var parsedMessageId))
            return NotFound();

        var result = await sender.Send(
            new HideMessageCommand(conversationId, parsedMessageId), cancellationToken);
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

    /// <summary>
    /// Toggles one of your reactions on a message and returns the emoji's resulting state.
    /// POST-shaped rather than PUT/DELETE because the caller's intent ("I want my reaction
    /// here") maps to a toggle: tapping a highlighted chip again takes it back. Anyone in the
    /// thread may react to any message in it, their own included — only edit and delete are
    /// the sender's alone. Both participants are pushed the change, so the recipient's open
    /// thread updates live.
    /// </summary>
    [HttpPost("{id}/messages/{messageId}/reactions")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleReaction(
        string id,
        string messageId,
        [FromBody] ToggleReactionRequest request,
        CancellationToken cancellationToken)
    {
        // Malformed ids are a 404, not a 400, on both segments — the same rule every other
        // messaging action uses.
        if (!Ulid.TryParse(id, out var conversationId) || !Ulid.TryParse(messageId, out var parsedMessageId))
            return NotFound();

        // A null or blank body element is a 400 here, unlike the ids: the ids are probing,
        // but an absent emoji is simply a malformed request from a real client.
        if (string.IsNullOrWhiteSpace(request.Emoji))
            return BadRequest(new { errors = new Dictionary<string, string[]> {
                [nameof(request.Emoji)] = ["A reaction is required."] } });

        var result = await sender.Send(
            new ToggleMessageReactionCommand(conversationId, parsedMessageId, request.Emoji),
            cancellationToken);
        return result.ToActionResult();
    }
}
