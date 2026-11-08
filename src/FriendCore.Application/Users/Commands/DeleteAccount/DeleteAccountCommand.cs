using ErrorOr;
using MediatR;

namespace FriendCore.Application.Users.Commands.DeleteAccount;

/// <summary>
///     Soft-deletes the signed-in account and revokes every refresh token. Content is retained
///     for the retention period; the global soft-delete filter hides the account immediately.
/// </summary>
public sealed record DeleteAccountCommand : IRequest<ErrorOr<Success>>;
