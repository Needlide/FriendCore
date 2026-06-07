using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Posts.Commands.CreatePost;
using FriendCore.Application.Posts.Commands.DeletePost;
using FriendCore.Domain.Posts;
using FriendCore.Domain.Users;
using FriendCore.UnitTests.Users;
using NSubstitute;

namespace FriendCore.UnitTests.Posts;

public class PostCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 7, 9, 0, 0, TimeSpan.Zero);

    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IPostRepository _postRepository = Substitute.For<IPostRepository>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly User _author = UserTestFactory.Create();

    public PostCommandHandlerTests()
    {
        _currentUserService.UserId.Returns(_author.Id);
        _dateTimeProvider.UtcNow.Returns(Now);
        _userRepository.GetByIdAsync(_author.Id, Arg.Any<CancellationToken>()).Returns(_author);
    }

    private CreatePostCommandHandler CreateHandler() =>
        new(_currentUserService, _userRepository, _postRepository, _dateTimeProvider);

    private DeletePostCommandHandler DeleteHandler() =>
        new(_currentUserService, _postRepository);

    [Fact]
    public async Task Create_ReturnsUnauthorized_WhenNoCurrentUser()
    {
        _currentUserService.UserId.Returns((Ulid?)null);

        var result = await CreateHandler().Handle(
            new CreatePostCommand("hello", ContentSensitivity.General),
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
            new CreatePostCommand("hello world", ContentSensitivity.Sensitive),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.NotNull(added);
        Assert.Equal("hello world", added!.Content);
        Assert.Equal(_author.Id, added.AuthorId);
        Assert.Equal(_author.Username, result.Value.AuthorUsername);
        Assert.Equal(nameof(ContentSensitivity.Sensitive), result.Value.Sensitivity);
        Assert.False(result.Value.IsLikedByCurrentUser);
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
