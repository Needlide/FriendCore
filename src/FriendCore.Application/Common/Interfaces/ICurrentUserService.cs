namespace FriendCore.Application.Common.Interfaces;

public interface ICurrentUserService
{
    Ulid? UserId { get; }

    bool IsAuthenticated { get; }

    // Remote IP of the current request, for the moderation audit log (§10.3). Null off-request.
    string? IpAddress { get; }
}