using FriendCore.Application.Moderation.Common;

namespace FriendCore.API.Contracts.Moderation;

public sealed record ReportRequest(ReportCategoryInput Category, string? Note = null);
