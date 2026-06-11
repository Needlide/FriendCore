using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Posts.Commands.AddComment;
using FriendCore.Application.Posts.Commands.DeleteComment;
using FriendCore.Application.Posts.Commands.LikePost;
using FriendCore.Application.Posts.Commands.UnlikePost;
using FriendCore.Domain.Posts;
using FriendCore.Domain.Users;
using FriendCore.UnitTests.Users;
using NSubstitute;

namespace FriendCore.UnitTests.Posts;

public class PostInteractionHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 7, 9, 0, 0, TimeSpan.Zero);

    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IPostRepository _postRepository = Substitute.For<IPostRepository>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly User _user = UserTestFactory.Create();

    public PostInteractionHandlerTests()
    {
        _currentUserService.UserId.Returns(_user.Id);
        _dateTimeProvider.UtcNow.Returns(Now);
        _userRepository.GetByIdAsync(_user.Id, Arg.Any<CancellationToken>()).Returns(_user);
    }

    private Post NewPost(out Post post)
    {
        post = Post.Create(Ulid.NewUlid(), "a post", ContentSensitivity.General, Now).Value;
        _postRepository.GetByIdAsync(post.Id, Arg.Any<CancellationToken>()).Returns(post);
        return post;
    }

    [Fact]
    public async Task Like_IncrementsCount_AndAddsLike_WhenNotAlreadyLiked()
    {
        NewPost(out var post);
        _postRepository.LikeExistsAsync(post.Id, _user.Id, Arg.Any<CancellationToken>()).Returns(false);

        var handler = new LikePostCommandHandler(_currentUserService, _postRepository, _dateTimeProvider);
        var result = await handler.Handle(new LikePostCommand(post.Id), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(1, post.LikeCount);
        Assert.True(result.Value.Liked);
        Assert.Equal(1, result.Value.LikeCount);
        await _postRepository.Received(1).AddLikeAsync(
            Arg.Is<Like>(l => l.PostId == post.Id && l.UserId == _user.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Like_IsNoOp_WhenAlreadyLiked()
    {
        NewPost(out var post);
        _postRepository.LikeExistsAsync(post.Id, _user.Id, Arg.Any<CancellationToken>()).Returns(true);

        var handler = new LikePostCommandHandler(_currentUserService, _postRepository, _dateTimeProvider);
        var result = await handler.Handle(new LikePostCommand(post.Id), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(0, post.LikeCount);
        Assert.True(result.Value.Liked);
        Assert.Equal(0, result.Value.LikeCount);
        await _postRepository.DidNotReceive().AddLikeAsync(Arg.Any<Like>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Unlike_DecrementsCount_AndRemovesLike_WhenLiked()
    {
        NewPost(out var post);
        post.IncrementLikeCount();
        _postRepository.LikeExistsAsync(post.Id, _user.Id, Arg.Any<CancellationToken>()).Returns(true);

        var handler = new UnlikePostCommandHandler(_currentUserService, _postRepository);
        var result = await handler.Handle(new UnlikePostCommand(post.Id), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(0, post.LikeCount);
        Assert.False(result.Value.Liked);
        Assert.Equal(0, result.Value.LikeCount);
        await _postRepository.Received(1).RemoveLikeAsync(post.Id, _user.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Like_ReturnsNotFound_WhenPostMissing()
    {
        _postRepository.GetByIdAsync(Arg.Any<Ulid>(), Arg.Any<CancellationToken>()).Returns((Post?)null);

        var handler = new LikePostCommandHandler(_currentUserService, _postRepository, _dateTimeProvider);
        var result = await handler.Handle(new LikePostCommand(Ulid.NewUlid()), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Post.NotFound", result.FirstError.Code);
    }

    [Fact]
    public async Task AddComment_IncrementsCommentCount_AndPersists()
    {
        NewPost(out var post);
        Comment? added = null;
        await _postRepository.AddCommentAsync(Arg.Do<Comment>(c => added = c), Arg.Any<CancellationToken>());

        var handler = new AddCommentCommandHandler(
            _currentUserService, _userRepository, _postRepository, _dateTimeProvider);
        var result = await handler.Handle(new AddCommentCommand(post.Id, "  nice post  "), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(1, post.CommentCount);
        Assert.NotNull(added);
        Assert.Equal("nice post", added!.Content);
        Assert.Equal("nice post", result.Value.Content);
        Assert.Equal(_user.Username, result.Value.AuthorUsername);
    }

    [Fact]
    public async Task DeleteComment_ReturnsForbidden_WhenNotAuthor()
    {
        var comment = Comment.Create(Ulid.NewUlid(), Ulid.NewUlid(), "not mine", Now).Value;
        _postRepository.GetCommentByIdAsync(comment.Id, Arg.Any<CancellationToken>()).Returns(comment);

        var handler = new DeleteCommentCommandHandler(_currentUserService, _postRepository);
        var result = await handler.Handle(new DeleteCommentCommand(comment.Id), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Post.CommentNotAuthor", result.FirstError.Code);
        await _postRepository.DidNotReceive().SoftDeleteCommentAsync(Arg.Any<Comment>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteComment_SoftDeletesAndDecrements_WhenAuthor()
    {
        NewPost(out var post);
        post.IncrementCommentCount();
        var comment = Comment.Create(post.Id, _user.Id, "mine", Now).Value;
        _postRepository.GetCommentByIdAsync(comment.Id, Arg.Any<CancellationToken>()).Returns(comment);

        var handler = new DeleteCommentCommandHandler(_currentUserService, _postRepository);
        var result = await handler.Handle(new DeleteCommentCommand(comment.Id), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(0, post.CommentCount);
        await _postRepository.Received(1).SoftDeleteCommentAsync(comment, Arg.Any<CancellationToken>());
    }
}
