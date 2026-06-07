using FriendCore.Domain.Posts;

namespace FriendCore.UnitTests.Posts;

public class PostTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 7, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_Succeeds_AndTrimsContent()
    {
        var authorId = Ulid.NewUlid();

        var result = Post.Create(authorId, "  hello world  ", ContentSensitivity.General, Now);

        Assert.False(result.IsError);
        Assert.Equal("hello world", result.Value.Content);
        Assert.Equal(authorId, result.Value.AuthorId);
        Assert.Equal(0, result.Value.LikeCount);
        Assert.Equal(0, result.Value.CommentCount);
        Assert.Equal(Now, result.Value.CreatedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_RequiresContent(string? content)
    {
        var result = Post.Create(Ulid.NewUlid(), content, ContentSensitivity.General, Now);

        Assert.True(result.IsError);
        Assert.Equal("Post.ContentRequired", result.FirstError.Code);
    }

    [Fact]
    public void Create_RejectsTooLongContent()
    {
        var content = new string('a', PostErrors.MaxContentLength + 1);

        var result = Post.Create(Ulid.NewUlid(), content, ContentSensitivity.General, Now);

        Assert.True(result.IsError);
        Assert.Equal("Post.ContentTooLong", result.FirstError.Code);
    }

    [Fact]
    public void LikeAndCommentCounts_NeverGoNegative()
    {
        var post = Post.Create(Ulid.NewUlid(), "hi", ContentSensitivity.General, Now).Value;

        post.IncrementLikeCount();
        post.DecrementLikeCount();
        post.DecrementLikeCount();
        post.IncrementCommentCount();
        post.DecrementCommentCount();
        post.DecrementCommentCount();

        Assert.Equal(0, post.LikeCount);
        Assert.Equal(0, post.CommentCount);
    }
}
