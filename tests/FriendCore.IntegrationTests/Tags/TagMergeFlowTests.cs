using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Tags;
using FriendCore.Domain.Users;
using FriendCore.IntegrationTests.Infrastructure;

namespace FriendCore.IntegrationTests.Tags;

public class TagMergeFlowTests(FriendCoreApiFactory factory) : IClassFixture<FriendCoreApiFactory>
{
    private readonly AuthScenarios _scenarios = new(factory);

    private async Task<HttpClient> ClientFor(string email, UserRole? role = null)
    {
        factory.Clock.Reset();
        var client = factory.CreateClient();
        var token = role is { } r
            ? await _scenarios.RegisterWithRoleAndGetToken(client, email, r)
            : await AuthScenarios.RegisterAndGetToken(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<string> CreateApprovedTag(HttpClient client, string name, string slug)
    {
        var response = await client.PostAsJsonAsync("/api/v1/tags", new { name });
        var body = await response.Content.ReadFromJsonAsync<TagResponse>();
        await _scenarios.ApproveTagAsync(slug);
        return body!.Id;
    }

    private static (string Name, string Slug) UniqueName(string prefix)
    {
        var suffix = Ulid.NewUlid().ToString().ToLowerInvariant()[..8];
        return ($"{prefix} {suffix}", $"{prefix}-{suffix}");
    }

    [Fact]
    public async Task SeniorModerator_MergesTags_SourceBecomesAliasResolvingToTarget()
    {
        var author = await ClientFor(AuthScenarios.UniqueEmail());
        var (sourceName, sourceSlug) = UniqueName("src");
        var (targetName, targetSlug) = UniqueName("tgt");
        var sourceId = await CreateApprovedTag(author, sourceName, sourceSlug);
        var targetId = await CreateApprovedTag(author, targetName, targetSlug);

        var senior = await ClientFor(AuthScenarios.UniqueEmail(), UserRole.SeniorModerator);
        var merge = await senior.PostAsJsonAsync($"/api/v1/tags/{sourceId}/merge", new { targetTagId = targetId });

        Assert.Equal(HttpStatusCode.OK, merge.StatusCode);

        await _scenarios.AssertTag(sourceSlug, tag =>
        {
            Assert.Equal(TagStatus.Merged, tag.Status);
            Assert.Equal(targetId, tag.MergedIntoId.ToString());
        });

        // Create-or-get by the old (source) name now returns the target.
        var resolved = await author.PostAsJsonAsync("/api/v1/tags", new { name = sourceName });
        var resolvedBody = await resolved.Content.ReadFromJsonAsync<TagResponse>();
        Assert.Equal(targetId, resolvedBody!.Id);
    }

    [Fact]
    public async Task Merge_RepointsProfileUsageToTarget()
    {
        var email = AuthScenarios.UniqueEmail();
        var user = await ClientFor(email);
        var (sourceName, sourceSlug) = UniqueName("psrc");
        var (targetName, targetSlug) = UniqueName("ptgt");
        var sourceId = await CreateApprovedTag(user, sourceName, sourceSlug);
        var targetId = await CreateApprovedTag(user, targetName, targetSlug);

        await user.PutAsJsonAsync("/api/v1/users/me/tags", new
        {
            tags = new[] { new { name = sourceName, tier = "Primary", isPublic = true } }
        });

        var senior = await ClientFor(AuthScenarios.UniqueEmail(), UserRole.SeniorModerator);
        var merge = await senior.PostAsJsonAsync($"/api/v1/tags/{sourceId}/merge", new { targetTagId = targetId });
        Assert.Equal(HttpStatusCode.OK, merge.StatusCode);

        await _scenarios.AssertProfileTags(email, tags =>
        {
            var tag = Assert.Single(tags);
            Assert.Equal(targetId, tag.TagId.ToString());
            Assert.Equal(TagTier.Primary, tag.Tier);
        });
        await _scenarios.AssertTag(targetSlug, tag => Assert.Equal(1, tag.ProfileCount));
        await _scenarios.AssertTag(sourceSlug, tag => Assert.Equal(0, tag.ProfileCount));
    }

    [Fact]
    public async Task Merge_ForbiddenForRegularModerator()
    {
        var author = await ClientFor(AuthScenarios.UniqueEmail());
        var (sourceName, sourceSlug) = UniqueName("fsrc");
        var (targetName, targetSlug) = UniqueName("ftgt");
        var sourceId = await CreateApprovedTag(author, sourceName, sourceSlug);
        var targetId = await CreateApprovedTag(author, targetName, targetSlug);

        var moderator = await ClientFor(AuthScenarios.UniqueEmail(), UserRole.Moderator);
        var merge = await moderator.PostAsJsonAsync($"/api/v1/tags/{sourceId}/merge", new { targetTagId = targetId });

        Assert.Equal(HttpStatusCode.Forbidden, merge.StatusCode);
    }

    [Fact]
    public async Task Merge_IntoNonActiveTarget_ReturnsBadRequest()
    {
        var author = await ClientFor(AuthScenarios.UniqueEmail());
        var (sourceName, sourceSlug) = UniqueName("nsrc");
        var sourceId = await CreateApprovedTag(author, sourceName, sourceSlug);

        // Target left PendingReview (not approved).
        var targetResponse = await author.PostAsJsonAsync("/api/v1/tags", new { name = UniqueName("ntgt").Name });
        var targetBody = await targetResponse.Content.ReadFromJsonAsync<TagResponse>();

        var senior = await ClientFor(AuthScenarios.UniqueEmail(), UserRole.SeniorModerator);
        var merge = await senior.PostAsJsonAsync($"/api/v1/tags/{sourceId}/merge", new { targetTagId = targetBody!.Id });

        Assert.Equal(HttpStatusCode.BadRequest, merge.StatusCode);
        Assert.Contains("Tag.InvalidMergeTarget", await merge.Content.ReadAsStringAsync());
    }
}
