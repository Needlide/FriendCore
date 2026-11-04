using System.Net;
using System.Net.Http.Json;
using FriendCore.Application.Common.Models;
using FriendCore.Infrastructure.Persistence;
using FriendCore.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FriendCore.IntegrationTests.Posts;

/// <summary>
///     Deleting a post takes its comments (soft-delete) and likes (hard remove) with it —
///     otherwise the rows survive invisibly behind the post's 404.
/// </summary>
public class PostDeletionCascadeFlowTests(FriendCoreApiFactory factory) : IClassFixture<FriendCoreApiFactory>
{
    private Task<(HttpClient Client, string Username)> RegisterClient() =>
        new AuthScenarios(factory).RegisterVerifiedClientAsync();

    private static async Task<string> CreatePost(HttpClient client, string content = "a post")
    {
        var response = await client.PostAsJsonAsync("/api/v1/posts", new { content, sensitivity = "General" });
        return (await response.Content.ReadFromJsonAsync<PostResponse>())!.Id;
    }

    private static async Task Comment(HttpClient client, string postId, string content)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/posts/{postId}/comments", new { content });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task Like(HttpClient client, string postId)
    {
        var response = await client.PostAsync($"/api/v1/posts/{postId}/like", content: null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<T> Run<T>(Func<AppDbContext, Task<T>> query)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await query(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    [Fact]
    public async Task Delete_SoftDeletesComments_AndRemovesLikes()
    {
        var (author, _) = await RegisterClient();
        var (commenter, _) = await RegisterClient();
        var postId = await CreatePost(author);
        await Comment(commenter, postId, "first!");
        await Comment(author, postId, "thanks");
        await Like(commenter, postId);
        await Like(author, postId);
        var id = Ulid.Parse(postId);

        var delete = await author.DeleteAsync($"/api/v1/posts/{postId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        // The post and its thread read as gone.
        Assert.Equal(HttpStatusCode.NotFound, (await author.GetAsync($"/api/v1/posts/{postId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await author.GetAsync($"/api/v1/posts/{postId}/comments")).StatusCode);

        // Database-level: both comments soft-deleted, both likes hard-removed.
        var deletedAts = await Run(db => db.Comments
            .IgnoreQueryFilters()
            .Where(comment => comment.PostId == id)
            .Select(comment => comment.DeletedAt)
            .ToListAsync());
        Assert.Equal(2, deletedAts.Count);
        Assert.All(deletedAts, deletedAt => Assert.NotNull(deletedAt));

        Assert.Equal(0, await Run(db => db.Likes.CountAsync(like => like.PostId == id)));
    }

    [Fact]
    public async Task Delete_CascadesComments_ByBlockedUsers()
    {
        var (author, _) = await RegisterClient();
        var (commenter, commenterName) = await RegisterClient();
        var postId = await CreatePost(author);
        await Comment(commenter, postId, "before the block");
        var id = Ulid.Parse(postId);

        // The block lands after the comment; the cascade must still reach it —
        // the repository bypasses the block filter for exactly this reason.
        Assert.Equal(HttpStatusCode.NoContent,
            (await author.PostAsync($"/api/v1/users/{commenterName}/block", content: null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await author.DeleteAsync($"/api/v1/posts/{postId}")).StatusCode);

        var deletedAt = await Run(db => db.Comments
            .IgnoreQueryFilters()
            .Where(comment => comment.PostId == id)
            .Select(comment => comment.DeletedAt)
            .SingleAsync());
        Assert.NotNull(deletedAt);
    }

    [Fact]
    public async Task Delete_LeavesAlreadyDeletedComments_Alone()
    {
        var (author, _) = await RegisterClient();
        var postId = await CreatePost(author);
        await Comment(author, postId, "deleted twice");
        var id = Ulid.Parse(postId);

        var list = await author.GetFromJsonAsync<CommentFeedResponse>($"/api/v1/posts/{postId}/comments");
        var commentId = list!.Comments.Single().Id;

        // Pin the clock so a cascade re-stamp would be distinguishable from the original.
        // Fixed whole-second instants: timestamptz round-trips them exactly, so the equality
        // below is a real assertion rather than a precision coin-flip.
        var firstDeleteAt = new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
        try
        {
            factory.Clock.Set(firstDeleteAt);
            Assert.Equal(HttpStatusCode.NoContent,
                (await author.DeleteAsync($"/api/v1/comments/{commentId}")).StatusCode);

            factory.Clock.Set(firstDeleteAt.AddHours(1));
            Assert.Equal(HttpStatusCode.NoContent,
                (await author.DeleteAsync($"/api/v1/posts/{postId}")).StatusCode);

            // The cascade must not re-stamp a comment the user deleted earlier.
            var deletedAt = await Run(db => db.Comments
                .IgnoreQueryFilters()
                .Where(comment => comment.PostId == id)
                .Select(comment => comment.DeletedAt)
                .SingleAsync());
            Assert.Equal(firstDeleteAt, deletedAt);
        }
        finally
        {
            factory.Clock.Reset();
        }
    }
}
