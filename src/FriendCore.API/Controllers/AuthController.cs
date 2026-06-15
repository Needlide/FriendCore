using FriendCore.API.Contracts.Auth;
using FriendCore.API.Extensions;
using FriendCore.Application.Common.Models;
using FriendCore.Application.Users.Commands.LoginUser;
using FriendCore.Application.Users.Commands.Logout;
using FriendCore.Application.Users.Commands.RefreshToken;
using FriendCore.Application.Users.Commands.RegisterUser;
using FriendCore.Application.Users.Commands.ResendVerificationEmail;
using FriendCore.Application.Users.Commands.VerifyEmail;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FriendCore.API.Controllers;

[ApiController]
[Route("api/v1/auth")]
[EnableRateLimiting(RateLimitingExtensions.AuthPolicy)]
public sealed class AuthController(ISender sender) : ControllerBase
{
    private const string RefreshCookieName = "fc_refresh";

    // Scoped to the auth routes so the refresh token is only sent where it's needed.
    private const string RefreshCookiePath = "/api/v1/auth";

    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new RegisterUserCommand(request.Username, request.DisplayName, request.Email, request.Password),
            cancellationToken);

        return Authenticated(result, StatusCodes.Status201Created);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new LoginUserCommand(request.Email, request.Password), cancellationToken);
        return Authenticated(result, StatusCodes.Status200OK);
    }

    [HttpPost("verify-email")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> VerifyEmail(
        [FromBody] VerifyEmailRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new VerifyEmailCommand(request.Token), cancellationToken);
        return Authenticated(result, StatusCodes.Status200OK);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        if (!Request.Cookies.TryGetValue(RefreshCookieName, out var token) || string.IsNullOrEmpty(token))
            return Unauthorized();

        var result = await sender.Send(new RefreshTokenCommand(token), cancellationToken);
        if (result.IsError)
        {
            ClearRefreshCookie();
            return ErrorOrExtensions.FromErrors(result.Errors);
        }

        SetRefreshCookie(result.Value.RefreshToken, result.Value.RefreshTokenExpiresAt);
        return Ok(result.Value.Auth);
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        if (Request.Cookies.TryGetValue(RefreshCookieName, out var token) && !string.IsNullOrEmpty(token))
            await sender.Send(new LogoutCommand(token), cancellationToken);

        ClearRefreshCookie();
        return NoContent();
    }

    [HttpPost("resend-verification")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ResendVerification(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ResendVerificationEmailCommand(), cancellationToken);

        return result.IsError
            ? ErrorOrExtensions.FromErrors(result.Errors)
            : NoContent();
    }

    // Sets the refresh cookie and returns only the body payload — the raw refresh token never
    // appears in the JSON response.
    private IActionResult Authenticated(ErrorOr.ErrorOr<AuthTokens> result, int successStatusCode)
    {
        if (result.IsError)
            return ErrorOrExtensions.FromErrors(result.Errors);

        SetRefreshCookie(result.Value.RefreshToken, result.Value.RefreshTokenExpiresAt);
        return successStatusCode == StatusCodes.Status201Created
            ? Created(string.Empty, result.Value.Auth)
            : Ok(result.Value.Auth);
    }

    private void SetRefreshCookie(string token, DateTimeOffset expiresAt)
    {
        Response.Cookies.Append(RefreshCookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps, // always true in prod (HTTPS-only); false over local http in tests/dev
            SameSite = SameSiteMode.Strict,
            Path = RefreshCookiePath,
            Expires = expiresAt,
            IsEssential = true // strictly-necessary auth cookie — exempt from consent (no banner)
        });
    }

    private void ClearRefreshCookie()
    {
        Response.Cookies.Delete(RefreshCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = RefreshCookiePath
        });
    }
}
