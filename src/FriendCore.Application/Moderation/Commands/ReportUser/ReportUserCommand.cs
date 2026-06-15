using ErrorOr;
using FriendCore.Application.Moderation.Common;
using MediatR;

namespace FriendCore.Application.Moderation.Commands.ReportUser;

public sealed record ReportUserCommand(string Username, ReportCategoryInput Category, string? Note)
    : IRequest<ErrorOr<Success>>;
