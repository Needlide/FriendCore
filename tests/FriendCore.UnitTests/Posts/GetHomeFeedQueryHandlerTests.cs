using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Options;
using FriendCore.Application.Posts.Common;
using FriendCore.Application.Posts.Queries.GetHomeFeed;
using FriendCore.Domain.Media;
using FriendCore.Domain.Posts;
using FriendCore.Domain.Users;
using FriendCore.UnitTests.Users;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace FriendCore.UnitTests.Posts;

public class GetHomeFeedQueryHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 7, 9, 0, 0, TimeSpan.Zero);

    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IPostRepository _postRepository = Substitute.For<IPostRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();

    private readonly User _viewer = UserTestFactory.Create(username: "viewer");

    public GetHomeFeedQueryHandlerTests()
    {
        _currentUserService.UserId.Returns(_viewer.Id);
        _userRepository.GetByIdAsync(_viewer.Id, Arg.Any<CancellationToken>()).Returns(_viewer);
    }

    private GetHomeFeedQueryHandler CreateHandler()
    {
        var mediaFactory = new PostMediaResponseFactory(
            Substitute.For<IMediaRepository>(),
            Options.Create(new StorageOptions { PublicBaseUrl = "http://storage.test/media" }));
        return new GetHomeFeedQueryHandler(_currentUserService, _postRepository, _userRepository, mediaFactory);
    }

    [Fact]
    public async Task Handle_ReturnsUnauthorized_WhenNoCurrentUser()
    {
        _currentUserService.UserId.Returns((Ulid?)null);

        var result = await CreateHandler().Handle(new GetHomeFeedQuery(null), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Common.Unauthorized", result.FirstError.Code);
    }

    [Fact]
    public async Task Handle_MapsPostsWithAuthorsAndLikedFlags()
    {
        var author = UserTestFactory.Create("bob");
        var post = Post.Create(author.Id, "hello", ContentSensitivity.General, Now).Value;

        _postRepository.GetHomeFeedAsync(
                _viewer.Id, Arg.Any<IReadOnlyList<string>>(), Arg.Any<ContentPreference>(),
                Arg.Any<(DateTimeOffset, Ulid)?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([post]);
        _userRepository.GetByIdsAsync(Arg.Any<IReadOnlyCollection<Ulid>>(), Arg.Any<CancellationToken>())
            .Returns([author]);
        _postRepository
            .GetLikedPostIdsAsync(_viewer.Id, Arg.Any<IReadOnlyCollection<Ulid>>(), Arg.Any<CancellationToken>())
            .Returns([post.Id]);

        var result = await CreateHandler().Handle(new GetHomeFeedQuery(null), CancellationToken.None);

        Assert.False(result.IsError);
        var item = Assert.Single(result.Value.Posts);
        Assert.Equal(post.Id.ToString(), item.Id);
        Assert.Equal("bob", item.AuthorUsername);
        Assert.True(item.IsLikedByCurrentUser);
        Assert.Null(result.Value.NextCursor);
    }
}