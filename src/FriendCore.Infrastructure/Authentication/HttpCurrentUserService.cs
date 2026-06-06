using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FriendCore.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace FriendCore.Infrastructure.Authentication;

public sealed class HttpCurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public Ulid? UserId
    {
        get
        {
            var subject = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? httpContextAccessor.HttpContext?.User.FindFirstValue(JwtRegisteredClaimNames.Sub);

            return subject is not null && Ulid.TryParse(subject, out var userId)
                ? userId
                : null;
        }
    }

    public bool IsAuthenticated =>
        httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;
}
