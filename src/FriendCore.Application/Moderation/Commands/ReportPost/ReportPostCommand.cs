using ErrorOr;
using FriendCore.Application.Moderation.Common;
using MediatR;

namespace FriendCore.Application.Moderation.Commands.ReportPost;

public sealed record ReportPostCommand(Ulid PostId, ReportCategoryInput Category, string? Note)
    : IRequest<ErrorOr<Success>>;
