namespace FriendCore.API.Contracts.Users;

/// <summary>
///     Optional body for POST /users/{username}/block. <see cref="Confirmed" /> is the client's
///     answer to a previous <c>User.BlockRequiresConfirmation</c> conflict (the target has a
///     follow relationship that blocking will sever).
/// </summary>
public sealed record BlockUserRequest(bool Confirmed);
