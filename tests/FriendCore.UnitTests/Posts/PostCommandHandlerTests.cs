using FriendCore.Application.Common;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Options;
using FriendCore.Application.Posts.Commands.CreatePost;
using FriendCore.Application.Posts.Commands.DeletePost;
using FriendCore.Application.Tags.Services;
using FriendCore.Domain.Media;
using FriendCore.Domain.Posts;
using FriendCore.Domain.Tags;
using FriendCore.Domain.Users;
using FriendCore.UnitTests.Users;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace FriendCore.UnitTests.Posts;

public class PostCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 7, 9, 0, 0, TimeSpan.Zero);

    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IPostRepository _postRepository = Substitute.For<IPostRepository>();
    private readonly IMediaRepository _mediaRepository = Substitute.For<IMediaRepository>();
    private readonly ITagRepository _tagRepository = Substitute.For<ITagRepository>();
    private readonly ITagNormalizer _tagNormalizer = Substitute.For<ITagNormalizer>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly User _author = UserTestFactory.CreateVerified();

    public PostCommandHandlerTests()
    {
        _currentUserService.UserId.Returns(_author.Id);
        _dateTimeProvider.UtcNow.Returns(Now);
        _userRepository.GetByIdAsync(_author.Id, Arg.Any<CancellationToken>()).Returns(_author);
        _mediaRepository.GetAttachedIdsAsync(Arg.Any<IReadOnlyCollection<Ulid>>(), Arg.Any<CancellationToken>())
            .Returns([]);
    }

    private CreatePostCommandHandler CreateHandler()
    {
        var mediaFactory = new MediaResponseFactory(
            _mediaRepository,
            Options.Create(new StorageOptions { PublicBaseUrl = "http://storage.test/media" }));
        return new(
            _currentUserService, _userRepository, _postRepository, _mediaRepository, _tagRepository,
            _tagNormalizer, mediaFactory, _dateTimeProvider);
    }

    private MediaItem ReadyMedia(Ulid? uploaderId = null)
    {
        var media = MediaItem.Create(
            uploaderId ?? _author.Id, "x.png", "image/png", "png", false, false, Now);
        media.MarkReady([
            MediaVariant.Create(
                MediaVariantType.Medium, MediaStorageKeys.Variant(media.Id, MediaVariantType.Medium), 800, 600, 1000)
        ]);
        return media;
    }

    private void MediaLookupReturns(params MediaItem[] items)
    {
        _mediaRepository.GetByIdsAsync(Arg.Any<IReadOnlyCollection<Ulid>>(), Arg.Any<CancellationToken>())
            .Returns(items);
    }

    private DeletePostCommandHandler DeleteHandler() =>
        new(_currentUserService, _postRepository, _tagRepository);

    [Fact]
    public async Task Create_ReturnsEmailNotVerified_WhenAuthorUnverified()
    {
        // §13.7 gate applies to posting only; comment/like handlers deliberately have no check.
        var unverified = UserTestFactory.Create(username: "unverified", email: "unverified@example.com");
        _currentUserService.UserId.Returns(unverified.Id);
        _userRepository.GetByIdAsync(unverified.Id, Arg.Any<CancellationToken>()).Returns(unverified);

        var result = await CreateHandler().Handle(
            new CreatePostCommand("hello", PostSensitivity.General, [], []),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("User.EmailNotVerified", result.FirstError.Code);
        await _postRepository.DidNotReceive().AddAsync(Arg.Any<Post>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_WithTags_AttachesResolvedTags_AndRecordsUsage()
    {
        var existing = Tag.Create("Art", "art", Ulid.NewUlid(), Now);
        _tagNormalizer.Normalize("Art").Returns(new NormalizedTag("Art", "art"));
        _tagRepository.GetBySlugResolvingAliasAsync("art", Arg.Any<CancellationToken>()).Returns(existing);
        Post? added = null;
        await _postRepository.AddAsync(Arg.Do<Post>(p => added = p), Arg.Any<CancellationToken>());

        var result = await CreateHandler().Handle(
            new CreatePostCommand("tagged", PostSensitivity.General, [], ["Art"]),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal([existing.Id], added!.Tags.Select(tag => tag.TagId));
        Assert.Equal(1, existing.PostCount);
    }

    [Fact]
    public async Task Create_WithUnknownTag_CreatesIt()
    {
        _tagNormalizer.Normalize("brand-new").Returns(new NormalizedTag("brand-new", "brand-new"));
        _tagRepository.GetBySlugResolvingAliasAsync("brand-new", Arg.Any<CancellationToken>())
            .Returns((Tag?)null);

        var result = await CreateHandler().Handle(
            new CreatePostCommand("tagged", PostSensitivity.General, [], ["brand-new"]),
            CancellationToken.None);

        Assert.False(result.IsError);
        await _tagRepository.Received(1).AddRangeAsync(
            Arg.Is<IReadOnlyCollection<Tag>>(tags => tags.Single().Slug == "brand-new"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_RejectsTooManyTags_BeforeResolvingAnything()
    {
        // Resolution persists newly created tags; an over-cap request must fail before that.
        var names = Enumerable.Range(0, TagLimits.MaxPostTags + 1).Select(i => $"tag-{i}").ToList();

        var result = await CreateHandler().Handle(
            new CreatePostCommand("tagged", PostSensitivity.General, [], names),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Post.TooManyTags", result.FirstError.Code);
        _tagNormalizer.DidNotReceiveWithAnyArgs().Normalize(default!);
    }

    [Fact]
    public async Task Delete_DecrementsTagUsage()
    {
        var tag = Tag.Create("Art", "art", Ulid.NewUlid(), Now);
        tag.RecordPostAdded(Now);
        var post = Post.Create(
            _author.Id, "tagged", ContentSensitivity.General, Now, tagIds: [tag.Id]).Value;
        _postRepository.GetByIdAsync(post.Id, Arg.Any<CancellationToken>()).Returns(post);
        _tagRepository.GetByIdsAsync(
                Arg.Is<IReadOnlyCollection<Ulid>>(ids => ids.Single() == tag.Id), Arg.Any<CancellationToken>())
            .Returns([tag]);

        var result = await DeleteHandler().Handle(new DeletePostCommand(post.Id), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(0, tag.PostCount);
    }

    [Fact]
    public async Task Create_ReturnsUnauthorized_WhenNoCurrentUser()
    {
        _currentUserService.UserId.Returns((Ulid?)null);

        var result = await CreateHandler().Handle(
            new CreatePostCommand("hello", PostSensitivity.General, [], []),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Common.Unauthorized", result.FirstError.Code);
    }

    [Fact]
    public async Task Create_PersistsPost_AndReturnsResponse()
    {
        Post? added = null;
        await _postRepository.AddAsync(Arg.Do<Post>(p => added = p), Arg.Any<CancellationToken>());

        var result = await CreateHandler().Handle(
            new CreatePostCommand("hello world", PostSensitivity.Sensitive, [], []),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.NotNull(added);
        Assert.Equal("hello world", added!.Content);
        Assert.Equal(_author.Id, added.AuthorId);
        Assert.Equal(_author.Username, result.Value.AuthorUsername);
        Assert.Equal(nameof(ContentSensitivity.Sensitive), result.Value.Sensitivity);
        Assert.False(result.Value.IsLikedByCurrentUser);
        Assert.Empty(result.Value.Media);
    }

    [Fact]
    public async Task Create_WithReadyOwnedMedia_AttachesInOrder_AndReturnsVariantUrls()
    {
        var first = ReadyMedia();
        var second = ReadyMedia();
        MediaLookupReturns(first, second);
        Post? added = null;
        await _postRepository.AddAsync(Arg.Do<Post>(p => added = p), Arg.Any<CancellationToken>());

        var result = await CreateHandler().Handle(
            new CreatePostCommand(null, PostSensitivity.General,
                [first.Id.ToString(), second.Id.ToString()], []),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Null(added!.Content); // image-only post
        Assert.Equal([first.Id, second.Id], added.Media.OrderBy(m => m.DisplayOrder).Select(m => m.MediaItemId));
        Assert.Equal(2, result.Value.Media.Count);
        Assert.Equal(first.Id.ToString(), result.Value.Media[0].Id);
        Assert.Equal(
            $"http://storage.test/media/uploads/variants/{first.Id}/medium.webp",
            result.Value.Media[0].Variants.Single().Url);
    }

    [Fact]
    public async Task Create_WithProcessingMedia_ReturnsNotReady()
    {
        var processing = MediaItem.Create(_author.Id, "x.png", "image/png", "png", false, false, Now);
        MediaLookupReturns(processing);

        var result = await CreateHandler().Handle(
            new CreatePostCommand("text", PostSensitivity.General, [processing.Id.ToString()], []),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Media.NotReady", result.FirstError.Code);
        await _postRepository.DidNotReceive().AddAsync(Arg.Any<Post>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_WithSomeoneElsesMedia_ReturnsNotOwner()
    {
        var foreign = ReadyMedia(Ulid.NewUlid());
        MediaLookupReturns(foreign);

        var result = await CreateHandler().Handle(
            new CreatePostCommand("text", PostSensitivity.General, [foreign.Id.ToString()], []),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Media.NotOwner", result.FirstError.Code);
    }

    [Fact]
    public async Task Create_WithUnknownMedia_ReturnsNotFound()
    {
        MediaLookupReturns(); // lookup yields nothing

        var result = await CreateHandler().Handle(
            new CreatePostCommand("text", PostSensitivity.General, [Ulid.NewUlid().ToString()], []),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Media.NotFound", result.FirstError.Code);
    }

    [Fact]
    public async Task Create_WithAlreadyAttachedMedia_ReturnsConflict()
    {
        var media = ReadyMedia();
        MediaLookupReturns(media);
        _mediaRepository.GetAttachedIdsAsync(Arg.Any<IReadOnlyCollection<Ulid>>(), Arg.Any<CancellationToken>())
            .Returns([media.Id]);

        var result = await CreateHandler().Handle(
            new CreatePostCommand("text", PostSensitivity.General, [media.Id.ToString()], []),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Media.AlreadyAttached", result.FirstError.Code);
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenMissing()
    {
        _postRepository.GetByIdAsync(Arg.Any<Ulid>(), Arg.Any<CancellationToken>()).Returns((Post?)null);

        var result = await DeleteHandler().Handle(new DeletePostCommand(Ulid.NewUlid()), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Post.NotFound", result.FirstError.Code);
    }

    [Fact]
    public async Task Delete_ReturnsForbidden_WhenNotAuthor()
    {
        var post = Post.Create(Ulid.NewUlid(), "someone else's", ContentSensitivity.General, Now).Value;
        _postRepository.GetByIdAsync(post.Id, Arg.Any<CancellationToken>()).Returns(post);

        var result = await DeleteHandler().Handle(new DeletePostCommand(post.Id), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Post.NotAuthor", result.FirstError.Code);
        await _postRepository.DidNotReceive().SoftDeleteAsync(Arg.Any<Post>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_SoftDeletes_WhenAuthor()
    {
        var post = Post.Create(_author.Id, "mine", ContentSensitivity.General, Now).Value;
        _postRepository.GetByIdAsync(post.Id, Arg.Any<CancellationToken>()).Returns(post);

        var result = await DeleteHandler().Handle(new DeletePostCommand(post.Id), CancellationToken.None);

        Assert.False(result.IsError);
        await _postRepository.Received(1).SoftDeleteAsync(post, Arg.Any<CancellationToken>());
    }
}
