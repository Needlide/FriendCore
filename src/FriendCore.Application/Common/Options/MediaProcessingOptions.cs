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

    /// <summary>
    ///     How many items may wait in the processing queue before uploads start blocking.
    /// </summary>
    /// <remarks>
    ///     The channel was unbounded until Oct 2026, which meant no backpressure: the API kept
    ///     accepting uploads the single-threaded processor could not keep up with and answered
    ///     "Processing" indefinitely, with nothing bounding how far behind it fell. Sized well
    ///     above any plausible MVP burst — this exists to make the ceiling a deliberate number
    ///     rather than the absence of one.
    /// </remarks>
    public int QueueCapacity { get; init; } = 100;
}
