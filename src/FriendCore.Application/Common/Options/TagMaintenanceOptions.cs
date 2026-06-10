namespace FriendCore.Application.Common.Options;

public sealed class TagMaintenanceOptions
{
    public const string SectionName = "TagMaintenance";

    // Max stale tags archived per query in the auto-archive drain loop.
    public int ArchiveBatchSize { get; init; } = 500;
}
