using FriendCore.API.Contracts.Users;
using FriendCore.API.Extensions;
using FriendCore.Application.Users.Commands.UpdateProfile;
using FriendCore.Application.Users.Queries.GetUserProfile;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FriendCore.API.Controllers;

[ApiController]
[Route("api/v1/users")]
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
}
