using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Media.Commands.DeleteMedia;
using FriendCore.Application.Media.Commands.UploadMedia;
using FriendCore.Application.Media.Queries.GetMediaStatus;
using FriendCore.Domain.Media;
using FriendCore.Domain.Posts;
using NSubstitute;

namespace FriendCore.UnitTests.Media;

public class MediaHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 11, 9, 0, 0, TimeSpan.Zero);

    private static readonly byte[] PngBytes =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52];

    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IMediaRepository _mediaRepository = Substitute.For<IMediaRepository>();
    private readonly IPostRepository _postRepository = Substitute.For<IPostRepository>();
    private readonly IStorageService _storageService = Substitute.For<IStorageService>();
    private readonly IMediaProcessingQueue _processingQueue = Substitute.For<IMediaProcessingQueue>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly Ulid _userId = Ulid.NewUlid();

    public MediaHandlerTests()
    {
        _currentUserService.UserId.Returns(_userId);
        _dateTimeProvider.UtcNow.Returns(Now);
        _mediaRepository.GetAttachedIdsAsync(Arg.Any<IReadOnlyCollection<Ulid>>(), Arg.Any<CancellationToken>())
            .Returns([]);
    }

    private UploadMediaCommandHandler UploadHandler() =>
        new(_currentUserService, _mediaRepository, _storageService, _processingQueue, _dateTimeProvider);

    private GetMediaStatusQueryHandler StatusHandler() =>
        new(_currentUserService, _mediaRepository);

    private static UploadMediaCommand Command(Stream content, string contentType = "image/png") =>
        new(content, content.Length, "photo.png", contentType, new Dictionary<string, bool>());

    [Fact]
    public async Task Upload_ReturnsUnauthorized_WhenNoCurrentUser()
    {
        _currentUserService.UserId.Returns((Ulid?)null);
        using var content = new MemoryStream(PngBytes);

        var result = await UploadHandler().Handle(Command(content), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Common.Unauthorized", result.FirstError.Code);
    }

    [Fact]
    public async Task Upload_RejectsHeaderMismatch_WithoutTouchingStorage()
    {
        // Declared PNG, actual JPEG bytes.
        using var content = new MemoryStream([0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0, 0, 0, 0, 0]);

        var result = await UploadHandler().Handle(Command(content), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Media.HeaderMismatch", result.FirstError.Code);
        await _storageService.DidNotReceiveWithAnyArgs()
            .UploadAsync(default!, default!, default!, default);
        await _mediaRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        await _processingQueue.DidNotReceiveWithAnyArgs().EnqueueAsync(default, default);
    }

    [Fact]
    public async Task Upload_StoresOriginal_AndPersistsProcessingItem()
    {
        MediaItem? added = null;
        string? uploadedKey = null;
        long uploadedStreamPosition = -1;
        await _mediaRepository.AddAsync(Arg.Do<MediaItem>(m => added = m), Arg.Any<CancellationToken>());
        await _storageService.UploadAsync(
            Arg.Do<string>(key => uploadedKey = key),
            Arg.Do<Stream>(s => uploadedStreamPosition = s.Position),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());

        using var content = new MemoryStream(PngBytes);
        var result = await UploadHandler().Handle(
            new UploadMediaCommand(
                content,
                content.Length,
                "photo.png",
                "IMAGE/PNG",
                new Dictionary<string, bool> { ["flashing"] = true }),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.NotNull(added);
        Assert.Equal(added!.OriginalStorageKey, uploadedKey);
        Assert.Equal(0, uploadedStreamPosition); // rewound after the magic-byte read
        Assert.Equal(_userId, added.UploaderId);
        Assert.Equal("image/png", added.ContentType); // normalised
        Assert.True(added.SafetyFlags.IsSet("flashing"));
        Assert.Equal(MediaStatus.Processing, added.Status);
        Assert.Equal(added.Id.ToString(), result.Value.Id);
        Assert.Equal("Processing", result.Value.Status);
        await _processingQueue.Received(1).EnqueueAsync(added.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Status_ReturnsNotFound_ForAnotherUsersMedia()
    {
        var media = MediaItem.Create(Ulid.NewUlid(), "x.png", "image/png", "png", MediaSafetyFlags.Empty, Now);
        _mediaRepository.GetByIdAsync(media.Id, Arg.Any<CancellationToken>()).Returns(media);

        var result = await StatusHandler().Handle(new GetMediaStatusQuery(media.Id), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Media.NotFound", result.FirstError.Code);
    }

    [Fact]
    public async Task Status_ReturnsStatus_ForOwner()
    {
        var media = MediaItem.Create(_userId, "x.png", "image/png", "png", MediaSafetyFlags.Empty, Now);
        _mediaRepository.GetByIdAsync(media.Id, Arg.Any<CancellationToken>()).Returns(media);

        var result = await StatusHandler().Handle(new GetMediaStatusQuery(media.Id), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal("Processing", result.Value.Status);
    }

    private DeleteMediaCommandHandler DeleteHandler() =>
        new(_currentUserService, _mediaRepository, _postRepository);

    [Fact]
    public async Task Delete_ReturnsNotFound_ForAnotherUsersMedia()
    {
        var media = MediaItem.Create(Ulid.NewUlid(), "x.png", "image/png", "png", MediaSafetyFlags.Empty, Now);
        _mediaRepository.GetByIdAsync(media.Id, Arg.Any<CancellationToken>()).Returns(media);

        var result = await DeleteHandler().Handle(new DeleteMediaCommand(media.Id), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Media.NotFound", result.FirstError.Code);
        await _mediaRepository.DidNotReceive().SoftDeleteAsync(Arg.Any<MediaItem>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_ReturnsConflict_WhenAttachedToPost()
    {
        var media = MediaItem.Create(_userId, "x.png", "image/png", "png", MediaSafetyFlags.Empty, Now);
        _mediaRepository.GetByIdAsync(media.Id, Arg.Any<CancellationToken>()).Returns(media);
        _mediaRepository.GetAttachedIdsAsync(Arg.Any<IReadOnlyCollection<Ulid>>(), Arg.Any<CancellationToken>())
            .Returns([media.Id]);

        var result = await DeleteHandler().Handle(new DeleteMediaCommand(media.Id), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Media.AttachedToPost", result.FirstError.Code);
        await _mediaRepository.DidNotReceive().SoftDeleteAsync(Arg.Any<MediaItem>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_SoftDeletes_OwnUnattachedMedia()
    {
        var media = MediaItem.Create(_userId, "x.png", "image/png", "png", MediaSafetyFlags.Empty, Now);
        _mediaRepository.GetByIdAsync(media.Id, Arg.Any<CancellationToken>()).Returns(media);

        var result = await DeleteHandler().Handle(new DeleteMediaCommand(media.Id), CancellationToken.None);

        Assert.False(result.IsError);
        await _mediaRepository.Received(1).SoftDeleteAsync(media, Arg.Any<CancellationToken>());
    }
}
