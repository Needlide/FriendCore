using FriendCore.Domain.Posts;
using FriendCore.Domain.Tags;

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
    public void Create_RequiresContentOrMedia(string? content)
    {
        var result = Post.Create(Ulid.NewUlid(), content, ContentSensitivity.General, Now);

        Assert.True(result.IsError);
        Assert.Equal("Post.ContentOrMediaRequired", result.FirstError.Code);
    }

    [Fact]
    public void Create_AllowsImageOnlyPost_WithNullContent()
    {
        var mediaIds = new[] { Ulid.NewUlid(), Ulid.NewUlid() };

        var result = Post.Create(Ulid.NewUlid(), "   ", ContentSensitivity.General, Now, mediaIds);

        Assert.False(result.IsError);
        Assert.Null(result.Value.Content);
        Assert.Equal(2, result.Value.Media.Count);
    }

    [Fact]
    public void Create_AssignsDisplayOrder_FromMediaIdOrder()
    {
        var mediaIds = new[] { Ulid.NewUlid(), Ulid.NewUlid(), Ulid.NewUlid() };

        var post = Post.Create(Ulid.NewUlid(), "with media", ContentSensitivity.General, Now, mediaIds).Value;

        Assert.Equal(mediaIds, post.Media.OrderBy(m => m.DisplayOrder).Select(m => m.MediaItemId));
        Assert.Equal([0, 1, 2], post.Media.Select(m => m.DisplayOrder).Order());
        Assert.All(post.Media, m => Assert.Equal(post.Id, m.PostId));
    }

    [Fact]
    public void Create_RejectsMoreThanMaxMedia()
    {
        var mediaIds = Enumerable.Range(0, PostErrors.MaxMediaPerPost + 1).Select(_ => Ulid.NewUlid()).ToList();

        var result = Post.Create(Ulid.NewUlid(), null, ContentSensitivity.General, Now, mediaIds);

        Assert.True(result.IsError);
        Assert.Equal("Post.TooManyMedia", result.FirstError.Code);
    }

    [Fact]
    public void Create_RejectsDuplicateMedia()
    {
        var mediaId = Ulid.NewUlid();

        var result = Post.Create(Ulid.NewUlid(), null, ContentSensitivity.General, Now, [mediaId, mediaId]);

        Assert.True(result.IsError);
        Assert.Equal("Post.DuplicateMedia", result.FirstError.Code);
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
    public void Create_AttachesTags_InOrder()
    {
        var tagIds = new[] { Ulid.NewUlid(), Ulid.NewUlid() };

        var result = Post.Create(Ulid.NewUlid(), "tagged", ContentSensitivity.General, Now, tagIds: tagIds);

        Assert.False(result.IsError);
        Assert.Equal(tagIds, result.Value.Tags.Select(tag => tag.TagId));
        Assert.All(result.Value.Tags, tag => Assert.Equal(result.Value.Id, tag.PostId));
    }

    [Fact]
    public void Create_RejectsMoreThanMaxTags()
    {
        var tagIds = Enumerable.Range(0, TagLimits.MaxPostTags + 1).Select(_ => Ulid.NewUlid()).ToList();

        var result = Post.Create(Ulid.NewUlid(), "tagged", ContentSensitivity.General, Now, tagIds: tagIds);

        Assert.True(result.IsError);
        Assert.Equal("Post.TooManyTags", result.FirstError.Code);
    }

    [Fact]
    public void Create_RejectsDuplicateTags()
    {
        var tagId = Ulid.NewUlid();

        var result = Post.Create(
            Ulid.NewUlid(), "tagged", ContentSensitivity.General, Now, tagIds: [tagId, tagId]);

        Assert.True(result.IsError);
        Assert.Equal("Post.DuplicateTags", result.FirstError.Code);
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
