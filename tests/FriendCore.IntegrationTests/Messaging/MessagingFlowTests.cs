using System.Net;
using System.Net.Http.Json;
using FriendCore.Application.Common.Models;
using FriendCore.IntegrationTests.Infrastructure;

namespace FriendCore.IntegrationTests.Messaging;

/// <summary>
///     Spec §5.6/§11 messaging REST surface (no SignalR yet): find-or-create conversations, send
///     messages, read receipts, and the §8 block intersection (blocked = conversation 404s).
/// </summary>
public class MessagingFlowTests(FriendCoreApiFactory factory) : IClassFixture<FriendCoreApiFactory>
{
    private Task<(HttpClient Client, string Username)> RegisterClient() =>
        new AuthScenarios(factory).RegisterVerifiedClientAsync();

    private static async Task<ConversationResponse> StartConversation(HttpClient client, string username)
    {
        var response = await client.PostAsJsonAsync("/api/v1/conversations", new { username });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ConversationResponse>())!;
    }

    private static async Task<MessageResponse> SendMessage(HttpClient client, string conversationId, string content)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/v1/conversations/{conversationId}/messages", new { content });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<MessageResponse>())!;
    }

    [Fact]
    public async Task StartConversation_IsFindOrCreate()
    {
        var (alice, _) = await RegisterClient();
        var (_, bobName) = await RegisterClient();

        var first = await StartConversation(alice, bobName);
        var second = await StartConversation(alice, bobName);

        // Same conversation returned, not a duplicate.
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(bobName, first.OtherParticipant.Username);
    }

    [Fact]
    public async Task SendAndRead_FlowsBetweenParticipants_AndDrivesUnreadCounts()
    {
        var (alice, aliceName) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);

        await SendMessage(alice, conversation.Id, "hey bob");
        await SendMessage(alice, conversation.Id, "you there?");

        // Bob sees two messages, newest first, and an unread count of 2 on his side.
        var bobMessages = await bob.GetFromJsonAsync<MessageFeedResponse>(
            $"/api/v1/conversations/{conversation.Id}/messages");
        Assert.Equal(2, bobMessages!.Messages.Count);
        Assert.Equal("you there?", bobMessages.Messages[0].Content);

        var bobConversations = await bob.GetFromJsonAsync<ConversationListResponse>("/api/v1/conversations");
        var bobView = Assert.Single(bobConversations!.Conversations);
        Assert.Equal(aliceName, bobView.OtherParticipant.Username);
        Assert.Equal(2, bobView.UnreadCount);

        // Alice's own sent messages never count as unread for her.
        var aliceConversations = await alice.GetFromJsonAsync<ConversationListResponse>("/api/v1/conversations");
        Assert.Equal(0, Assert.Single(aliceConversations!.Conversations).UnreadCount);

        // After Bob marks the conversation read, his unread count clears.
        var read = await bob.PostAsync($"/api/v1/conversations/{conversation.Id}/read", content: null);
        Assert.Equal(HttpStatusCode.NoContent, read.StatusCode);

        var afterRead = await bob.GetFromJsonAsync<ConversationListResponse>("/api/v1/conversations");
        Assert.Equal(0, Assert.Single(afterRead!.Conversations).UnreadCount);
    }

    [Fact]
    public async Task Outsider_CannotReadOrSend_GetsNotFound()
    {
        var (alice, _) = await RegisterClient();
        var (_, bobName) = await RegisterClient();
        var (mallory, _) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);

        var read = await mallory.GetAsync($"/api/v1/conversations/{conversation.Id}/messages");
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);

        var send = await mallory.PostAsJsonAsync(
            $"/api/v1/conversations/{conversation.Id}/messages", new { content = "intruding" });
        Assert.Equal(HttpStatusCode.NotFound, send.StatusCode);
    }

    [Fact]
    public async Task StartConversation_WithSelf_ReturnsBadRequest()
    {
        var (alice, aliceName) = await RegisterClient();

        var response = await alice.PostAsJsonAsync("/api/v1/conversations", new { username = aliceName });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task StartConversation_WithUnknownUser_ReturnsNotFound()
    {
        var (alice, _) = await RegisterClient();

        var response = await alice.PostAsJsonAsync(
            "/api/v1/conversations", new { username = AuthScenarios.NewUsername() });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Block_AfterConversation_HidesItAndStopsMessages()
    {
        var (alice, _) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);
        await SendMessage(alice, conversation.Id, "before the block");

        // Alice blocks Bob. The conversation must vanish for both and reject new messages (§8.1).
        var block = await alice.PostAsync($"/api/v1/users/{bobName}/block", content: null);
        Assert.Equal(HttpStatusCode.NoContent, block.StatusCode);

        var aliceList = await alice.GetFromJsonAsync<ConversationListResponse>("/api/v1/conversations");
        Assert.Empty(aliceList!.Conversations);

        var bobSend = await bob.PostAsJsonAsync(
            $"/api/v1/conversations/{conversation.Id}/messages", new { content = "still there?" });
        Assert.Equal(HttpStatusCode.NotFound, bobSend.StatusCode);

        var bobRead = await bob.GetAsync($"/api/v1/conversations/{conversation.Id}/messages");
        Assert.Equal(HttpStatusCode.NotFound, bobRead.StatusCode);
    }

    [Fact]
    public async Task EmptyMessage_ReturnsBadRequest()
    {
        var (alice, _) = await RegisterClient();
        var (_, bobName) = await RegisterClient();
        var conversation = await StartConversation(alice, bobName);

        var response = await alice.PostAsJsonAsync(
            $"/api/v1/conversations/{conversation.Id}/messages", new { content = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Conversations_RequireAuth()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/conversations");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
