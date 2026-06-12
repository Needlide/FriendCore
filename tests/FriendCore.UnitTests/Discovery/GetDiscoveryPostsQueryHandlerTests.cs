using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Application.Common.Options;
using FriendCore.Application.Discovery.Queries.GetDiscoveryPosts;
using FriendCore.Application.Posts.Common;
using FriendCore.Domain.Discovery;
using FriendCore.Domain.Media;
using FriendCore.Domain.Posts;
using FriendCore.Domain.Users;
using FriendCore.UnitTests.Users;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace FriendCore.UnitTests.Discovery;

public class GetDiscoveryPostsQueryHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 12, 9, 0, 0, TimeSpan.Zero);

    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IDiscoveryRepository _discoveryRepository = Substitute.For<IDiscoveryRepository>();
    private readonly IPostRepository _postRepository = Substitute.For<IPostRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IFollowRepository _followRepository = Substitute.For<IFollowRepository>();
    private readonly IMuteRepository _muteRepository = Substitute.For<IMuteRepository>();
    private readonly ICacheService _cacheService = Substitute.For<ICacheService>();
    private readonly Ulid _userId = Ulid.NewUlid();

    public GetDiscoveryPostsQueryHandlerTests()
    {
        _currentUserService.UserId.Returns(_userId);
        _followRepository.GetFollowedIdsAsync(_userId, Arg.Any<CancellationToken>()).Returns([]);
        _muteRepository.GetMutedIdsAsync(_userId, Arg.Any<CancellationToken>()).Returns([]);
        _postRepository.GetLikedPostIdsAsync(_userId, Arg.Any<IReadOnlyCollection<Ulid>>(),
            Arg.Any<CancellationToken>()).Returns([]);
    }

    private GetDiscoveryPostsQueryHandler CreateHandler()
    {
        var mediaFactory = new PostMediaResponseFactory(
            Substitute.For<IMediaRepository>(),
            Options.Create(new StorageOptions { PublicBaseUrl = "http://storage.test/media" }));
        return new GetDiscoveryPostsQueryHandler(
            _currentUserService, _discoveryRepository, _postRepository, _userRepository,
            _followRepository, _muteRepository, mediaFactory, _cacheService);
    }

    private void CacheReturns(params Ulid[] postIds)
    {
        _cacheService.GetOrSetAsync(
                $"discovery:posts:{_userId}",
                Arg.Any<Func<CancellationToken, Task<List<string>>>>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<CancellationToken>())
            .Returns(postIds.Select(id => id.ToString()).ToList());
    }

    private (Post Post, User Author) PostBy(string username, string content)
    {
        var author = UserTestFactory.Create(username: username);
        var post = Post.Create(author.Id, content, ContentSensitivity.General, Now).Value;
        return (post, author);
    }

    [Fact]
    public async Task Handle_ReturnsUnauthorized_WhenNoCurrentUser()
    {
        _currentUserService.UserId.Returns((Ulid?)null);

        var result = await CreateHandler().Handle(new GetDiscoveryPostsQuery(), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Common.Unauthorized", result.FirstError.Code);
    }

    [Fact]
    public async Task Handle_RehydratesInCachedOrder_AndDropsMissingPosts()
    {
        var (first, firstAuthor) = PostBy("first", "one");
        var (second, secondAuthor) = PostBy("second", "two");
        var deletedId = Ulid.NewUlid();

        // The cache still references a post that no longer survives the global filters
        // (deleted or block-hidden) — rehydration must drop it without complaint.
        CacheReturns(second.Id, deletedId, first.Id);
        _postRepository.GetByIdsAsync(Arg.Any<IReadOnlyCollection<Ulid>>(), Arg.Any<CancellationToken>())
            .Returns([first, second]);
        _userRepository.GetByIdsAsync(Arg.Any<IReadOnlyCollection<Ulid>>(), Arg.Any<CancellationToken>())
            .Returns([firstAuthor, secondAuthor]);

        var result = await CreateHandler().Handle(new GetDiscoveryPostsQuery(), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(["two", "one"], result.Value.Posts.Select(p => p.Content).ToArray());
    }

    [Fact]
    public async Task Handle_DropsPostsFromFollowedOrMutedAuthors_AtServeTime()
    {
        var (followedPost, _) = PostBy("nowfollowed", "followed content");
        var (mutedPost, _) = PostBy("nowmuted", "muted content");
        var (keptPost, keptAuthor) = PostBy("kept", "kept content");

        CacheReturns(followedPost.Id, mutedPost.Id, keptPost.Id);
        _postRepository.GetByIdsAsync(Arg.Any<IReadOnlyCollection<Ulid>>(), Arg.Any<CancellationToken>())
            .Returns([followedPost, mutedPost, keptPost]);
        _userRepository.GetByIdsAsync(Arg.Any<IReadOnlyCollection<Ulid>>(), Arg.Any<CancellationToken>())
            .Returns([keptAuthor]);
        _followRepository.GetFollowedIdsAsync(_userId, Arg.Any<CancellationToken>())
            .Returns([followedPost.AuthorId]);
        _muteRepository.GetMutedIdsAsync(_userId, Arg.Any<CancellationToken>())
            .Returns([mutedPost.AuthorId]);

        var result = await CreateHandler().Handle(new GetDiscoveryPostsQuery(), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal("kept content", Assert.Single(result.Value.Posts).Content);
    }

    [Fact]
    public async Task Handle_ReturnsEmpty_WhenCacheHasNoIds()
    {
        CacheReturns();

        var result = await CreateHandler().Handle(new GetDiscoveryPostsQuery(), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Empty(result.Value.Posts);
        await _postRepository.DidNotReceiveWithAnyArgs().GetByIdsAsync(default!, default);
    }
}
