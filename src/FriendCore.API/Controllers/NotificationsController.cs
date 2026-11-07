using FriendCore.API.Extensions;
using FriendCore.Application.Common.Models;
using FriendCore.Application.Notifications.Commands.DeleteNotification;
using FriendCore.Application.Notifications.Commands.MarkNotificationRead;
using FriendCore.Application.Notifications.Commands.MarkNotificationsRead;
using FriendCore.Application.Notifications.Queries.GetNotifications;
using FriendCore.Application.Notifications.Queries.GetUnreadCount;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FriendCore.API.Controllers;

[ApiController]
[Route("api/v1/notifications")]
[EnableRateLimiting(RateLimitingExtensions.ApiPolicy)]
public sealed class NotificationsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetNotifications(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetNotificationsQuery(), cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("unread-count")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetUnreadCount(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetUnreadCountQuery(), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("read")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MarkRead(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new MarkNotificationsReadCommand(), cancellationToken);
        return result.IsError ? ErrorOrExtensions.FromErrors(result.Errors) : NoContent();
    }

    [HttpPost("{id}/read")]
    [Authorize]
    [ProducesResponseType(typeof(UnreadCountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkRead(string id, CancellationToken cancellationToken)
    {
        if (!Ulid.TryParse(id, out var notificationId))
            return NotFound();

        var result = await sender.Send(new MarkNotificationReadCommand(notificationId), cancellationToken);
        return result.ToActionResult();
    }

    [HttpDelete("{id}")]
    [Authorize]
    [ProducesResponseType(typeof(UnreadCountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        if (!Ulid.TryParse(id, out var notificationId))
            return NotFound();

        var result = await sender.Send(new DeleteNotificationCommand(notificationId), cancellationToken);
        return result.ToActionResult();
    }
}
