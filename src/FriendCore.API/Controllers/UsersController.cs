using FriendCore.API.Contracts.Moderation;
using FriendCore.API.Contracts.Tags;
using FriendCore.API.Contracts.Users;
using FriendCore.API.Extensions;
using FriendCore.Application.Moderation.Commands.ReportUser;
using FriendCore.Application.Tags.Commands.UpdateProfileTags;
using FriendCore.Application.Tags.Queries.GetMyProfileTags;
using FriendCore.Application.Users.Commands.BlockUser;
using FriendCore.Application.Users.Commands.ClearNotificationSnooze;
using FriendCore.Application.Users.Commands.FollowUser;
using FriendCore.Application.Users.Commands.MuteUser;
using FriendCore.Application.Users.Commands.SetNotificationSnooze;
using FriendCore.Application.Users.Commands.UnblockUser;
using FriendCore.Application.Users.Commands.UnfollowUser;
using FriendCore.Application.Users.Commands.UnmuteUser;
using FriendCore.Application.Users.Commands.UpdateKeywordFilters;
using FriendCore.Application.Users.Commands.UpdateNotificationTypes;
using FriendCore.Application.Users.Commands.UpdatePreferences;
using FriendCore.Application.Users.Commands.UpdateProfile;
using FriendCore.Application.Users.Queries.GetBlocks;
using FriendCore.Application.Users.Queries.GetFollowers;
using FriendCore.Application.Users.Queries.GetFollowing;
using FriendCore.Application.Users.Queries.GetKeywordFilters;
using FriendCore.Application.Users.Queries.GetMutes;
using FriendCore.Application.Users.Queries.GetNotificationSnooze;
using FriendCore.Application.Users.Queries.GetNotificationTypes;
using FriendCore.Application.Users.Queries.GetPreferences;
using FriendCore.Application.Users.Queries.GetUserProfile;
using FriendCore.Application.Users.Queries.GetUserTags;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;

namespace FriendCore.API.Controllers;

[ApiController]
[Route("api/v1/users")]
[EnableRateLimiting(RateLimitingExtensions.ApiPolicy)]
public sealed class UsersController(ISender sender) : ControllerBase
{
    [HttpGet("{username}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfile(string username, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetUserProfileQuery(username), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPatch("me/profile")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UpdateProfileCommand(request.DisplayName, request.Bio, request.WebsiteUrl, request.Location),
            cancellationToken);

        return result.ToActionResult();
    }

    [HttpPut("me/tags")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateTags(UpdateProfileTagsRequest request, CancellationToken cancellationToken)
    {
        var tags = request.Tags
            .Select(tag => new ProfileTagInput(tag.Name, tag.Tier, tag.IsPublic))
            .ToList();

        var result = await sender.Send(new UpdateProfileTagsCommand(tags), cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("me/tags")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetTags(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetMyProfileTagsQuery(), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("{username}/follow")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Follow(string username, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new FollowUserCommand(username), cancellationToken);
        return result.IsError ? ErrorOrExtensions.FromErrors(result.Errors) : NoContent();
    }

    [HttpDelete("{username}/follow")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unfollow(string username, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new UnfollowUserCommand(username), cancellationToken);
        return result.IsError ? ErrorOrExtensions.FromErrors(result.Errors) : NoContent();
    }

    [HttpPost("{username}/block")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Block(
        string username,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)]
        BlockUserRequest? request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new BlockUserCommand(username, request?.Confirmed ?? false),
            cancellationToken);

        return result.IsError ? ErrorOrExtensions.FromErrors(result.Errors) : NoContent();
    }

    [HttpDelete("{username}/block")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unblock(string username, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new UnblockUserCommand(username), cancellationToken);
        return result.IsError ? ErrorOrExtensions.FromErrors(result.Errors) : NoContent();
    }

    [HttpPost("{username}/mute")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Mute(string username, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new MuteUserCommand(username), cancellationToken);
        return result.IsError ? ErrorOrExtensions.FromErrors(result.Errors) : NoContent();
    }

    [HttpDelete("{username}/mute")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unmute(string username, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new UnmuteUserCommand(username), cancellationToken);
        return result.IsError ? ErrorOrExtensions.FromErrors(result.Errors) : NoContent();
    }

    [HttpPatch("me/preferences")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdatePreferences(
        [FromBody] UpdatePreferencesRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UpdatePreferencesCommand(
                request.HideFlashingMedia,
                request.HideArachnidMedia,
                request.ReduceMotion,
                request.DisableAutoplay,
                request.ReduceVisualNoise,
                request.SendMessageOnEnter,
                request.ContentPreference),
            cancellationToken);

        return result.ToActionResult();
    }

    [HttpGet("me/preferences")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetPreferences(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetPreferencesQuery(), cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("me/keyword-filters")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetKeywordFilters(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetKeywordFiltersQuery(), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPatch("me/keyword-filters")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateKeywordFilters(
        [FromBody] UpdateKeywordFiltersRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new UpdateKeywordFiltersCommand(request.Keywords), cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("me/notification-types")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetNotificationTypes(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetNotificationTypesQuery(), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPatch("me/notification-types")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateNotificationTypes(
        [FromBody] UpdateNotificationTypesRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new UpdateNotificationTypesCommand(request.MutedTypes), cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("me/snooze")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetNotificationSnooze(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetNotificationSnoozeQuery(), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("me/snooze")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SetNotificationSnooze(
        [FromBody] SetNotificationSnoozeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new SetNotificationSnoozeCommand(request.Until), cancellationToken);
        return result.ToActionResult();
    }

    [HttpDelete("me/snooze")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ClearNotificationSnooze(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ClearNotificationSnoozeCommand(), cancellationToken);
        return result.IsError ? ErrorOrExtensions.FromErrors(result.Errors) : NoContent();
    }

    [HttpPost("{username}/report")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Report(
        string username,
        [FromBody] ReportRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ReportUserCommand(username, request.Category, request.Note), cancellationToken);
        return result.IsError ? ErrorOrExtensions.FromErrors(result.Errors) : Accepted();
    }

    [HttpGet("me/blocks")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetBlocks(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetBlocksQuery(), cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("me/mutes")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMutes(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetMutesQuery(), cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("{username}/tags")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserTags(string username, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetUserTagsQuery(username), cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("{username}/followers")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Followers(string username, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetFollowersQuery(username), cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("{username}/following")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Following(string username, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetFollowingQuery(username), cancellationToken);
        return result.ToActionResult();
    }
}
