using FriendCore.API.Contracts.Auth;
using FriendCore.API.Extensions;
using FriendCore.Application.Users.Commands.LoginUser;
using FriendCore.Application.Users.Commands.RegisterUser;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FriendCore.API.Controllers;

[ApiController]
[Route("api/v1/auth")]
[AllowAnonymous]
[EnableRateLimiting(RateLimitingExtensions.AuthPolicy)]
public sealed class AuthController(ISender sender) : ControllerBase
{
    [HttpPost("register")]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new RegisterUserCommand(request.Username, request.DisplayName, request.Email, request.Password),
            cancellationToken);

        return result.ToCreatedResult();
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new LoginUserCommand(request.Email, request.Password),
            cancellationToken);

        return result.ToActionResult();
    }
}