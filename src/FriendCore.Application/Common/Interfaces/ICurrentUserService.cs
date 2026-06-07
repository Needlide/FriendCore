namespace FriendCore.Application.Common.Interfaces;

public interface ICurrentUserService
{
    Ulid? UserId { get; }

    bool IsAuthenticated { get; }
}