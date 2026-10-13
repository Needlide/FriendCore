using System.Threading.Channels;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Options;
using Microsoft.Extensions.Options;

namespace FriendCore.Infrastructure.MediaProcessing;

/// <summary>
///     In-process work queue (MVP stand-in for Hangfire, like TagMaintenanceService). The channel
///     is in-memory only — restart durability comes from the MediaProcessingSweeper re-enqueueing
///     anything left in Processing, not from the queue itself.
/// </summary>
/// <remarks>
///     <para>
///         Bounded since Oct 2026. <c>CreateUnbounded</c> looks like the safer choice — it never
///         rejects — but it is the absence of a limit rather than a large one: the producer keeps
///         accepting work a single-threaded consumer cannot keep up with, and the queue becomes an
///         invisible backlog whose only symptom is a growing pile of "Processing" rows.
///     </para>
///     <para>
///         <c>FullMode.Wait</c> is the default and is kept deliberately: <c>DropWrite</c> would let
///         <c>TryWrite</c> fail silently, and a dropped item would sit in Processing until the
///         sweeper failed it minutes later — a confusing failure rather than an honest one.
///         Blocking the upload instead is backpressure, and at this capacity it never blocks in
///         practice. SingleReader stays true: concurrent consumers are a separate decision, and the
///         loop already scopes a DbContext per item so they would be safe.
///     </para>
/// </remarks>
internal sealed class MediaProcessingQueue : IMediaProcessingQueue
{
    private readonly Channel<Ulid> _channel;

    public MediaProcessingQueue(IOptions<MediaProcessingOptions> options)
    {
        // Floor of 1: a zero capacity would reject every write and wedge the pipeline entirely.
        var capacity = Math.Max(1, options.Value.QueueCapacity);

        _channel = Channel.CreateBounded<Ulid>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            FullMode = BoundedChannelFullMode.Wait,
        });
    }

    public ChannelReader<Ulid> Reader => _channel.Reader;

    public async Task EnqueueAsync(Ulid mediaId, CancellationToken cancellationToken = default)
    {
        await _channel.Writer.WriteAsync(mediaId, cancellationToken);
    }
}
