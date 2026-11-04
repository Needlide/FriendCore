using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Application.Common.Options;
using FriendCore.Application.Media.Commands.RevealMedia;
using FriendCore.Domain.Common;
using FriendCore.Domain.Media;
using FriendCore.Domain.Messaging;
using FriendCore.Domain.Posts;
using FriendCore.Domain.Users;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace FriendCore.UnitTests.Media;

/// <summary>
/// The §7.4 consent gate. Every case here is one that, if it were wrong, hands a photosensitivity
/// hazard — or a private DM image — to someone who should not have it.
/// </summary>
public class RevealMediaHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IMediaRepository _mediaRepository = Substitute.For<IMediaRepository>();
    private readonly IPostRepository _postRepository = Substitute.For<IPostRepository>();
    private readonly IMessagingRepository _messagingRepository = Substitute.For<IMessagingRepository>();
    private readonly IBlockRepository _blockRepository = Substitute.For<IBlockRepository>();

    private readonly Ulid _viewerId = Ulid.NewUlid();
    private readonly Ulid _uploaderId = Ulid.NewUlid();
    private readonly Ulid _partnerId = Ulid.NewUlid();
    private readonly Ulid _postId = Ulid.NewUlid();
    private readonly Ulid _messageId = Ulid.NewUlid();

    private Conversation _conversation = null!;

    public RevealMediaHandlerTests()
    {
        _currentUserService.UserId.Returns(_viewerId);
        _conversation = Conversation.CreateDirect(_viewerId, _partnerId, Now).Value;
    }

    private RevealMediaCommandHandler Handler() =>
        new(_currentUserService, _mediaRepository, _postRepository, _messagingRepository,
            _blockRepository, Options.Create(new StorageOptions { PublicBaseUrl = "https://cdn.test" }));

    // Variants are attached by ProcessMediaJob once the renditions exist, so a reveal test has
    // to finalise the item or it would be asserting against an empty variant list.
    private static MediaItem Ready(MediaItem media)
    {
        // Variants are attached by ProcessMediaJob once the renditions exist, so a reveal test
        // has to finalise the item or it would assert against an empty variant list.
        media.MarkReady(
        [
            MediaVariant.Create(MediaVariantType.Small, $"{media.Id}/small.webp", 320, 240, 1024),
            MediaVariant.Create(MediaVariantType.Medium, $"{media.Id}/medium.webp", 800, 600, 4096)
        ]);
        return media;
    }

    private static MediaItem FlaggedMedia(Ulid uploaderId) =>
        Ready(MediaItem.Create(
            uploaderId, "x.png", "image/png", "png",
            MediaSafetyFlags.From(("flashing", true)), Now));

    private static MediaItem PlainMedia(Ulid uploaderId) =>
        Ready(MediaItem.Create(
            uploaderId, "x.png", "image/png", "png",
            MediaSafetyFlags.Empty, Now));

    private void Arrange(MediaItem media, MediaAttachment attachment)
    {
        _mediaRepository.GetByIdAsync(media.Id, Arg.Any<CancellationToken>()).Returns(media);
        _mediaRepository.GetAttachmentAsync(media.Id, Arg.Any<CancellationToken>()).Returns(attachment);
    }

    private Task<ErrorOr<MediaVariantsResponse>> Reveal(Ulid mediaId) =>
        Handler().Handle(new RevealMediaCommand(mediaId), CancellationToken.None);

    [Fact]
    public async Task ReturnsUnauthorized_WhenNoCurrentUser()
    {
        _currentUserService.UserId.Returns((Ulid?)null);

        var result = await Reveal(Ulid.NewUlid());

        Assert.Equal("Common.Unauthorized", result.FirstError.Code);
    }

    [Fact]
    public async Task ReturnsNotFound_ForMissingMedia()
    {
        _mediaRepository.GetByIdAsync(Arg.Any<Ulid>(), Arg.Any<CancellationToken>()).Returns((MediaItem?)null);

        var result = await Reveal(Ulid.NewUlid());

        Assert.Equal("Media.NotFound", result.FirstError.Code);
    }

    [Fact]
    public async Task UnflaggedMedia_IsServedWithoutAnyAttachmentLookup()
    {
        // Flagged media is the only thing ever withheld, so the ordinary path must not pay for
        // an authorization round trip it does not need.
        var media = PlainMedia(_uploaderId);
        _mediaRepository.GetByIdAsync(media.Id, Arg.Any<CancellationToken>()).Returns(media);

        var result = await Reveal(media.Id);

        Assert.False(result.IsError);
        await _mediaRepository.DidNotReceiveWithAnyArgs().GetAttachmentAsync(default, default);
    }

    [Fact]
    public async Task Uploader_MayReveal_TheirOwnFlaggedUpload()
    {
        var media = FlaggedMedia(_viewerId);
        Arrange(media, MediaAttachment.Unattached);

        var result = await Reveal(media.Id);

        Assert.False(result.IsError);
        Assert.NotEmpty(result.Value.Variants);
    }

    [Fact]
    public async Task Reader_MayReveal_FlaggedMediaOnAPostTheyCanSee()
    {
        var media = FlaggedMedia(_uploaderId);
        Arrange(media, new MediaAttachment(_postId, null, null));
        var post = Post.Create(_uploaderId, "look", ContentSensitivity.General, Now).Value;
        _postRepository.GetByIdAsync(_postId, Arg.Any<CancellationToken>()).Returns(post);

        var result = await Reveal(media.Id);

        Assert.False(result.IsError);
        Assert.NotEmpty(result.Value.Variants);
    }

    [Theory]
    [InlineData("arachnophobia")]
    [InlineData("explicit")]
    public async Task ConsentFlaggedMedia_IsAuthorizedLikeAnyOtherGatedMedia(string flag)
    {
        // The reveal endpoint must recognize every flag that can withhold a URL, not just the
        // original flashing/motion pair. Missing one turns the placeholder's "Show media" button
        // into a 404 for media the reader is actually allowed to see.
        var media = Ready(MediaItem.Create(
            _uploaderId, "x.png", "image/png", "png",
            MediaSafetyFlags.From((flag, true)), Now));
        Arrange(media, new MediaAttachment(_postId, null, null));
        var post = Post.Create(_uploaderId, "look", ContentSensitivity.General, Now).Value;
        _postRepository.GetByIdAsync(_postId, Arg.Any<CancellationToken>()).Returns(post);

        var result = await Reveal(media.Id);

        Assert.False(result.IsError);
        Assert.NotEmpty(result.Value.Variants);
        Assert.Equal(flag == "explicit" ? "Explicit" : "Arachnophobia", result.Value.GateReason);
    }

    [Theory]
    [InlineData("arachnophobia")]
    [InlineData("explicit")]
    public async Task Stranger_MayNotReveal_ConsentFlaggedMediaAttachedToNothing(string flag)
    {
        var media = Ready(MediaItem.Create(
            _uploaderId, "x.png", "image/png", "png",
            MediaSafetyFlags.From((flag, true)), Now));
        Arrange(media, MediaAttachment.Unattached);

        var result = await Reveal(media.Id);

        Assert.Equal("Media.NotFound", result.FirstError.Code);
    }

    [Fact]
    public async Task Reader_MayNotReveal_FlaggedMediaOnAPostTheyCannotSee()
    {
        // Block-filtered lookup returns null for a post you cannot see, which is the same 404
        // every other post surface gives (§8.2).
        var media = FlaggedMedia(_uploaderId);
        Arrange(media, new MediaAttachment(_postId, null, null));
        _postRepository.GetByIdAsync(_postId, Arg.Any<CancellationToken>()).Returns((Post?)null);

        var result = await Reveal(media.Id);

        Assert.Equal("Media.NotFound", result.FirstError.Code);
    }

    [Fact]
    public async Task Participant_MayReveal_FlaggedMediaInTheirConversation()
    {
        var media = FlaggedMedia(_partnerId);
        Arrange(media, new MediaAttachment(null, _messageId, _conversation.Id));
        _messagingRepository.GetByIdAsync(_conversation.Id, Arg.Any<CancellationToken>())
            .Returns(_conversation);

        var result = await Reveal(media.Id);

        Assert.False(result.IsError);
    }

    [Fact]
    public async Task Outsider_MayNotReveal_FlaggedMediaInSomeoneElsesConversation()
    {
        // The case this whole design exists for: an id leak must not turn into a DM image.
        var media = FlaggedMedia(_partnerId);
        Arrange(media, new MediaAttachment(null, _messageId, _conversation.Id));
        _messagingRepository.GetByIdAsync(_conversation.Id, Arg.Any<CancellationToken>())
            .Returns(_conversation);
        _currentUserService.UserId.Returns(Ulid.NewUlid());

        var result = await Reveal(media.Id);

        // 404, not 403 — confirming the id exists would itself be a disclosure.
        Assert.Equal("Media.NotFound", result.FirstError.Code);
    }

    [Fact]
    public async Task BlockedParticipant_MayNotReveal_FlaggedMediaInThatConversation()
    {
        var media = FlaggedMedia(_partnerId);
        Arrange(media, new MediaAttachment(null, _messageId, _conversation.Id));
        _messagingRepository.GetByIdAsync(_conversation.Id, Arg.Any<CancellationToken>())
            .Returns(_conversation);
        _blockRepository.ExistsBetweenAsync(_viewerId, _partnerId, Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await Reveal(media.Id);

        Assert.Equal("Media.NotFound", result.FirstError.Code);
    }

    [Fact]
    public async Task Stranger_MayNotReveal_FlaggedMediaAttachedToNothing()
    {
        // Unattached but flagged and not yours — a draft someone else orphaned. Not disclosable.
        var media = FlaggedMedia(_uploaderId);
        Arrange(media, MediaAttachment.Unattached);

        var result = await Reveal(media.Id);

        Assert.Equal("Media.NotFound", result.FirstError.Code);
    }

    [Fact]
    public async Task RevealedVariants_AreTheSameUrlsAListResponseWouldHaveGiven()
    {
        var media = FlaggedMedia(_uploaderId);
        Arrange(media, new MediaAttachment(_postId, null, null));
        var post = Post.Create(_uploaderId, "look", ContentSensitivity.General, Now).Value;
        _postRepository.GetByIdAsync(_postId, Arg.Any<CancellationToken>()).Returns(post);

        var result = await Reveal(media.Id);

        // Not a different, more permissive set: the same variant URLs, minus the gate.
        var ungated = MediaResponse.Create(
            media, "https://cdn.test", includeVariants: true);
        Assert.Equal(ungated.Variants.Count, result.Value.Variants.Count);
        Assert.Contains(result.Value.Variants, v => v.Url.StartsWith("https://cdn.test/"));
        // The original is never in a variant list (§7.3).
        Assert.DoesNotContain(result.Value.Variants, v => v.Type == "Original");
    }
}
