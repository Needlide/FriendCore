namespace FriendCore.Application.Common.Options;

public sealed class MediaProcessingOptions
{
    public const string SectionName = "MediaProcessing";

    /// <summary>How often the sweeper looks for items stuck in Processing.</summary>
    public int SweepIntervalSeconds { get; init; } = 300;

    /// <summary>
    ///     How long an item may sit in Processing before the sweeper re-enqueues it. Must comfortably
    ///     exceed the slowest realistic processing time, or items get processed twice (harmless —
    ///     MarkReady is idempotent — but wasteful).
    /// </summary>
    public int StuckAfterMinutes { get; init; } = 10;

    /// <summary>Lossy WebP quality (1–100) for generated variants.</summary>
    public int WebpQuality { get; init; } = 80;
}
