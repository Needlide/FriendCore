using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Users;
using FriendCore.Infrastructure.Persistence;
using FriendCore.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FriendCore.IntegrationTests.Messaging;

/// <summary>
///     Support inbox (POST /api/v1/conversations/support): find-or-create 1:1 with the oldest
///     Admin, usable USB for thanks / bug reports / quick questions. The help channel bypasses
///     the block system — blocking the admin is rejected so the channel can't be severed.
/// </summary>
public class SupportConversationFlowTests(FriendCoreApiFactory factory) : IClassFixture<FriendCoreApiFactory>
{
    private readonly AuthScenarios _scenarios = new(factory);

    // The class shares one database across methods, so reuse the oldest admin when one already
    // exists instead of promoting a second — keeps admin resolution deterministic.
    private async Task<(HttpClient Client, string Username)> EnsureAdminAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var existing = await db.Users
            .IgnoreQueryFilters()
            .Where(u => u.Role == UserRole.Admin)
            .OrderBy(u => u.CreatedAt)
            .FirstOrDefaultAsync();

        if (existing is not null)
        {
            var client = factory.CreateClient();
            var login = await AuthScenarios.LoginByUsername(client, existing.Username);
            var body = await login.Content.ReadFromJsonAsync<AuthResponse>();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", body!.AccessToken);
            return (client, existing.Username);
        }

        factory.Clock.Reset();
        var adminClient = factory.CreateClient();
        var email = AuthScenarios.UniqueEmail();
        var username = AuthScenarios.NewUsername();
        await AuthScenarios.Register(adminClient, email, username);
        await _scenarios.PromoteUserAsync(email, UserRole.Admin);
        var adminLogin = await _scenarios.LoginAsync(adminClient, email);
        var adminBody = await adminLogin.Content.ReadFromJsonAsync<AuthResponse>();
        adminClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminBody!.AccessToken);
        return (adminClient, username);
    }

    private Task<(HttpClient Client, string Username)> RegisterClient() =>
        _scenarios.RegisterVerifiedClientAsync();

    [Fact]
    public async Task Support_IsFindOrCreate_WithAdminCounterpart()
    {
        var (_, adminName) = await EnsureAdminAsync();
        var (user, _) = await RegisterClient();

        var first = await user.PostAsync("/api/v1/conversations/support", content: null);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var firstBody = (await first.Content.ReadFromJsonAsync<ConversationResponse>())!;

        var second = await user.PostAsync("/api/v1/conversations/support", content: null);
        var secondBody = (await second.Content.ReadFromJsonAsync<ConversationResponse>())!;

        Assert.Equal(firstBody.Id, secondBody.Id);
        Assert.Equal(adminName, firstBody.OtherParticipant.Username);
    }

    [Fact]
    public async Task Support_FullChatFlow_UserAndAdminExchangeMessages()
    {
        var (admin, _) = await EnsureAdminAsync();
        var (user, _) = await RegisterClient();

        var start = await user.PostAsync("/api/v1/conversations/support", content: null);
        var conversation = (await start.Content.ReadFromJsonAsync<ConversationResponse>())!;

        var send = await user.PostAsJsonAsync(
            $"/api/v1/conversations/{conversation.Id}/messages", new { content = "thanks for building this" });
        Assert.Equal(HttpStatusCode.Created, send.StatusCode);

        var adminMessages = await admin.GetFromJsonAsync<MessageFeedResponse>(
            $"/api/v1/conversations/{conversation.Id}/messages");
        Assert.Contains(adminMessages!.Messages, m => m.Content == "thanks for building this");

        var reply = await admin.PostAsJsonAsync(
            $"/api/v1/conversations/{conversation.Id}/messages", new { content = "you are welcome" });
        Assert.Equal(HttpStatusCode.Created, reply.StatusCode);

        var userMessages = await user.GetFromJsonAsync<MessageFeedResponse>(
            $"/api/v1/conversations/{conversation.Id}/messages");
        Assert.Contains(userMessages!.Messages, m => m.Content == "you are welcome");
    }

    [Fact]
    public async Task Support_BlockingAdmin_IsRejected_AndChannelSurvives()
    {
        var (_, adminName) = await EnsureAdminAsync();
        var (user, _) = await RegisterClient();

        var block = await user.PostAsync($"/api/v1/users/{adminName}/block", content: null);
        Assert.Equal(HttpStatusCode.BadRequest, block.StatusCode);
        Assert.Contains("User.CannotBlockAdmin", await block.Content.ReadAsStringAsync());

        var start = await user.PostAsync("/api/v1/conversations/support", content: null);
        Assert.Equal(HttpStatusCode.Created, start.StatusCode);
    }

    [Fact]
    public async Task Support_RequiresAuth()
    {
        var response = await factory.CreateClient().PostAsync("/api/v1/conversations/support", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
