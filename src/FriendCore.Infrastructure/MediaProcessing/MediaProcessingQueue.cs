using System.Threading.Channels;
using FriendCore.Application.Common.Interfaces;

namespace FriendCore.Infrastructure.MediaProcessing;

/// <summary>
///     In-process work queue (MVP stand-in for Hangfire, like TagMaintenanceService). The channel
///     is in-memory only — restart durability comes from the MediaProcessingSweeper re-enqueueing
///     anything left in Processing, not from the queue itself.
/// </summary>
internal sealed class MediaProcessingQueue : IMediaProcessingQueue
{
    private readonly Channel<Ulid> _channel = Channel.CreateUnbounded<Ulid>(
        new UnboundedChannelOptions { SingleReader = true });

    public ChannelReader<Ulid> Reader => _channel.Reader;

    public void Enqueue(Ulid mediaId)
    {
        // Unbounded channel: TryWrite only fails after writer completion, which never happens.
        _channel.Writer.TryWrite(mediaId);
    }
}
