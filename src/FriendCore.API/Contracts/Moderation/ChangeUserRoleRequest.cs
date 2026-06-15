using FriendCore.Application.Moderation.Common;

namespace FriendCore.API.Contracts.Moderation;

public sealed record ChangeUserRoleRequest(UserRoleInput Role, string Reason);
