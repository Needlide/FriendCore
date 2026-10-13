using System.Security.Cryptography;
using System.Threading.Channels;
using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Application.Common.Options;
using FriendCore.Application.Media.Commands.UploadMedia;
using FriendCore.Domain.Common;
using FriendCore.Domain.Media;
using FriendCore.Infrastructure.MediaProcessing;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace FriendCore.UnitTests.Media;

/// <summary>
/// The content hash column and the bounded processing queue. Both are prerequisites rather than
/// features: nothing consumes the hash yet, so what matters is that it is real, correctly
/// derived, and never faked from the client. The queue's job is to have a ceiling at all.
/// </summary>
public class MediaContentHashAndQueueTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 21, 0, 0, TimeSpan.Zero);

    /// <summary>PNG magic bytes — the upload path rejects anything whose header does not match.</summary>
    private static readonly byte[] PngHeader =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IMediaRepository _mediaRepository = Substitute.For<IMediaRepository>();
    private readonly IStorageService _storageService = Substitute.For<IStorageService>();
    private readonly IMediaProcessingQueue _processingQueue = Substitute.For<IMediaProcessingQueue>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();

    private MediaItem? _added;

    public MediaContentHashAndQueueTests()
    {
        _currentUserService.UserId.Returns(Ulid.NewUlid());
        _dateTimeProvider.UtcNow.Returns(Now);
        _mediaRepository
            .AddAsync(Arg.Any<MediaItem>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                _added = ci.Arg<MediaItem>();
                return Task.CompletedTask;
            });
    }

    private UploadMediaCommandHandler Handler() =>
        new(_currentUserService, _mediaRepository, _storageService, _processingQueue, _dateTimeProvider);

    private Task<ErrorOr<MediaStatusResponse>> Upload(byte[] bytes, string contentType = "image/png")
    {
        var content = new MemoryStream(bytes);
        return Handler().Handle(
            new UploadMediaCommand(content, content.Length, "photo.png", contentType, false, false),
            CancellationToken.None);
    }

    // --- the hash -----------------------------------------------------------------------------

    [Fact]
    public async Task Upload_StoresTheSha256OfTheBytesAsReceived()
    {
        var bytes = TestBytes(256, 1);

        await Upload(bytes);

        // Computed server-side from the stream, so it is verifiable against the bytes directly.
        var expected = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        Assert.Equal(expected, _added!.ContentHash);
        Assert.Equal(64, _added.ContentHash!.Length);
        // Lowercase throughout — mixed-case hex would break a string comparison against a
        // hash computed by anything else, and CSAM databases compare as lowercase.
        Assert.Equal(_added.ContentHash, _added.ContentHash!.ToLowerInvariant());
    }

    [Fact]
    public async Task IdenticalBytes_HashIdentically_AcrossUploads()
    {
        var bytes = TestBytes(128, 7);
        await Upload(bytes);
        var first = _added!.ContentHash;

        await Upload(bytes);

        Assert.Equal(first, _added!.ContentHash);
    }

    [Fact]
    public async Task DifferentBytes_HashDifferently()
    {
        await Upload(TestBytes(128, 7));
        var first = _added!.ContentHash;

        await Upload(TestBytes(128, 8));

        Assert.NotEqual(first, _added!.ContentHash);
    }

    [Fact]
    public async Task TheHashIsComputedBeforeStorage_SoStorageStillGetsTheWholeFile()
    {
        // Hashing consumes the stream. If it were not rewound, the object in storage would be a
        // truncated file that looks fine until ProcessMediaJob fails to decode it.
        var bytes = TestBytes(512, 3);

        await Upload(bytes);

        await _storageService.Received(1).UploadAsync(
            Arg.Any<string>(),
            Arg.Is<Stream>(stream => stream.Length == bytes.Length && stream.Position == 0),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AHeaderMismatch_NeverReachesTheHashOrStorage()
    {
        // Bytes that are not the claimed type: rejected before anything is written, so no
        // half-processed row and no orphaned object.
        await Upload("this is not a png"u8.ToArray(), contentType: "image/png");

        Assert.Null(_added);
        await _storageService.DidNotReceiveWithAnyArgs()
            .UploadAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task TheUploadEnqueues_ByAwaitingTheBoundedQueue()
    {
        await Upload(TestBytes(64, 1));

        // Awaited, and carrying the request's cancellation token so a disconnecting client
        // releases a writer that is waiting for room.
        await _processingQueue.Received(1).EnqueueAsync(_added!.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Create_AcceptsAnAbsentHash_ForRowsThatPredateTheColumn()
    {
        // The column is nullable precisely because existing originals live in object storage and
        // cannot be backfilled. Null means "unknown" — a consumer must treat it as never-matching,
        // not as a shared value.
        var media = MediaItem.Create(
            Ulid.NewUlid(), "old.png", "image/png", "png", false, false, Now);

        Assert.Null(media.ContentHash);
    }

    // --- the bounded queue -----------------------------------------------------------------------

    private static MediaProcessingQueue Queue(int capacity) =>
        new(Options.Create(new MediaProcessingOptions { QueueCapacity = capacity }));

    [Fact]
    public async Task TheQueue_DrainsWhatItAccepts()
    {
        var queue = Queue(4);
        var id = Ulid.NewUlid();

        await queue.EnqueueAsync(id);

        Assert.True(queue.Reader.TryRead(out var read));
        Assert.Equal(id, read);
    }

    [Fact]
    public async Task TheQueue_BlocksOnceFull_RatherThanDroppingOrGrowing()
    {
        var queue = Queue(2);
        await queue.EnqueueAsync(Ulid.NewUlid());
        await queue.EnqueueAsync(Ulid.NewUlid());

        // Capacity reached. The next write must WAIT: it must not return as though it succeeded
        // (dropwrite would lose the item, leaving a row stuck in Processing until the sweeper
        // failed it minutes later) and must not be accepted by an unbounded buffer.
        var pending = queue.EnqueueAsync(Ulid.NewUlid());
        Assert.False(pending.IsCompleted);

        // Draining one releases exactly one waiter.
        Assert.True(queue.Reader.TryRead(out _));
        await pending.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task TheQueue_HonoursCancellation_SoAWaiterIsNotStuckForever()
    {
        var queue = Queue(1);
        await queue.EnqueueAsync(Ulid.NewUlid());

        using var cts = new CancellationTokenSource();
        var pending = queue.EnqueueAsync(Ulid.NewUlid(), cts.Token);
        Assert.False(pending.IsCompleted);

        // A client that disconnects mid-wait must not hold a writer until capacity appears.
        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }

    [Fact]
    public async Task TheQueue_ClampsAZeroCapacity()
    {
        // Zero capacity would reject every write and wedge the pipeline entirely; a config typo
        // must not be able to do that.
        var queue = Queue(0);

        await queue.EnqueueAsync(Ulid.NewUlid());

        Assert.True(queue.Reader.TryRead(out _));
    }

    /// <summary>
    ///     Deterministic pseudo-random bytes behind a valid PNG header, so a "different file"
    ///     really differs while still passing the content-type gate.
    /// </summary>
    private static byte[] TestBytes(int length, int seed)
    {
        var bytes = new byte[length];
        new Random(seed).NextBytes(bytes);
        PngHeader.CopyTo(bytes, 0);
        return bytes;
    }
}