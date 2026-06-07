using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Posts.Queries.GetHomeFeed;
using FriendCore.Domain.Posts;
using FriendCore.Domain.Users;
using FriendCore.UnitTests.Users;
using NSubstitute;

namespace FriendCore.UnitTests.Posts;

public class GetHomeFeedQueryHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 7, 9, 0, 0, TimeSpan.Zero);

    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IPostRepository _postRepository = Substitute.For<IPostRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly Ulid _userId = Ulid.NewUlid();

    public GetHomeFeedQueryHandlerTests()
    {
        _currentUserService.UserId.Returns(_userId);
    }

    private GetHomeFeedQueryHandler CreateHandler() =>
        new(_currentUserService, _postRepository, _userRepository);

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
        var author = UserTestFactory.Create(username: "bob");
        var post = Post.Create(author.Id, "hello", ContentSensitivity.General, Now).Value;

        _postRepository.GetHomeFeedAsync(_userId, Arg.Any<DateTimeOffset?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([post]);
        _userRepository.GetByIdsAsync(Arg.Any<IReadOnlyCollection<Ulid>>(), Arg.Any<CancellationToken>())
            .Returns([author]);
        _postRepository.GetLikedPostIdsAsync(_userId, Arg.Any<IReadOnlyCollection<Ulid>>(), Arg.Any<CancellationToken>())
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
