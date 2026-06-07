namespace FriendCore.Application.Common.Models;

public sealed record TagModerationQueueResponse(IReadOnlyList<TagModerationQueueItem> Tags);

public sealed record TagModerationQueueItem(
    string TagId,
    string Name,
    string Slug,
    DateTimeOffset CreatedAt,
    string CreatedById,
    string? CreatorUsername,
    string? CreatorDisplayName);
