using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Users;
using FriendCore.IntegrationTests.Infrastructure;

namespace FriendCore.IntegrationTests.Messaging;

/// <summary>
/// Spec §13.4/§13.5 applied to DMs: a recipient can report one message, and a moderator can
/// actually action it. The end-to-end path matters here — without a Message branch in the
/// enforcement ladder, a report could be filed but never resolved.
/// </summary>
public class ReportMessageFlowTests(FriendCoreApiFactory factory) : IClassFixture<FriendCoreApiFactory>
{
    private readonly AuthScenarios _scenarios = new(factory);

    private Task<(HttpClient Client, string Username)> RegisterClient() =>
        _scenarios.RegisterVerifiedClientAsync();

    /// <summary>Same, but also hands back the email — suspension is verified by re-login.</summary>
    private async Task<(HttpClient Client, string Username, string Email)> RegisterClientWithEmail()
    {
        factory.Clock.Reset();
        var client = factory.CreateClient();
        var email = AuthScenarios.UniqueEmail();
        var username = AuthScenarios.NewUsername();
        await AuthScenarios.Register(client, email, username);
        await _scenarios.MarkVerifiedAsync(email);
        var login = await _scenarios.LoginAsync(client, email);
        var body = await login.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        return (client, username, email);
    }

    private async Task<HttpClient> ModeratorClient()
    {
        factory.Clock.Reset();
        var client = factory.CreateClient();
        var token = await _scenarios.RegisterWithRoleAndGetToken(
            client, AuthScenarios.UniqueEmail(), UserRole.Moderator);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<ConversationResponse> StartConversation(HttpClient client, string username)
    {
        var response = await client.PostAsJsonAsync("/api/v1/conversations", new { username });
        return (await response.Content.ReadFromJsonAsync<ConversationResponse>())!;
    }

    private static async Task<MessageResponse> SendMessage(HttpClient client, string conversationId, string content)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/v1/conversations/{conversationId}/messages", new { content });
        return (await response.Content.ReadFromJsonAsync<MessageResponse>())!;
    }

    private static Task<HttpResponseMessage> Report(
        HttpClient client, string conversationId, string messageId, string category = "Harassment") =>
        client.PostAsJsonAsync(
            $"/api/v1/conversations/{conversationId}/messages/{messageId}/report",
            new { category, note = "this is not acceptable" });

    [Fact]
    public async Task ReportMessage_ThenAppearsInModeratorQueue()
    {
        var (sender, _) = await RegisterClient();
        var (recipient, senderName) = await RegisterClient();
        var conversation = await StartConversation(sender, senderName);
        var message = await SendMessage(sender, conversation.Id, "you are a awful person");

        var response = await Report(recipient, conversation.Id, message.Id);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var moderator = await ModeratorClient();
        var queue = await moderator.GetFromJsonAsync<ReportQueueResponse>("/api/v1/admin/reports");

        var entry = Assert.Single(queue!.Reports, r => r.TargetId == message.Id);
        Assert.Equal("Message", entry.TargetType);
        Assert.Equal("Harassment", entry.Category);
        // §13.5: harassment is yellow — queued above spam, below the high-severity categories.
        Assert.Equal("Medium", entry.Priority);
        Assert.Equal("Pending", entry.Status);
    }

    [Fact]
    public async Task ReportMessage_IsIdempotent_PerReporter()
    {
        var (sender, _) = await RegisterClient();
        var (recipient, senderName) = await RegisterClient();
        var conversation = await StartConversation(sender, senderName);
        var message = await SendMessage(sender, conversation.Id, "once is enough");

        Assert.Equal(HttpStatusCode.NoContent, (await Report(recipient, conversation.Id, message.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Report(recipient, conversation.Id, message.Id)).StatusCode);

        var moderator = await ModeratorClient();
        var queue = await moderator.GetFromJsonAsync<ReportQueueResponse>("/api/v1/admin/reports");
        Assert.Single(queue!.Reports, r => r.TargetId == message.Id);
    }

    [Fact]
    public async Task ReportingYourOwnMessage_IsRefused()
    {
        var (sender, _) = await RegisterClient();
        var (_, otherName) = await RegisterClient();
        var conversation = await StartConversation(sender, otherName);
        var message = await SendMessage(sender, conversation.Id, "drafting thoughts");

        var response = await Report(sender, conversation.Id, message.Id);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Outsider_CannotReport_AndLearnsNothing()
    {
        var (sender, _) = await RegisterClient();
        var (_, otherName) = await RegisterClient();
        var (mallory, _) = await RegisterClient();
        var conversation = await StartConversation(sender, otherName);
        var message = await SendMessage(sender, conversation.Id, "not yours to report");

        var response = await Report(mallory, conversation.Id, message.Id);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Moderator_CanRemoveAMessage_AndTheReportResolves()
    {
        var (sender, _) = await RegisterClient();
        var (recipient, senderName) = await RegisterClient();
        var conversation = await StartConversation(sender, senderName);
        var keep = await SendMessage(sender, conversation.Id, "harmless");
        var reported = await SendMessage(sender, conversation.Id, "abusive");

        Assert.Equal(HttpStatusCode.NoContent, (await Report(recipient, conversation.Id, reported.Id)).StatusCode);

        var moderator = await ModeratorClient();
        var queue = await moderator.GetFromJsonAsync<ReportQueueResponse>("/api/v1/admin/reports");
        var entry = Assert.Single(queue!.Reports, r => r.TargetId == reported.Id);

        // The whole reason for the Message branch in the ladder: without it this 400s and the
        // report sits in the queue forever.
        var resolved = await moderator.PostAsJsonAsync(
            $"/api/v1/admin/reports/{entry.Id}/resolve",
            new { action = "ContentRemoval", reason = "abusive DM" });
        Assert.Equal(HttpStatusCode.NoContent, resolved.StatusCode);

        // Gone for both sides; the rest of the thread is untouched.
        var feed = await recipient.GetFromJsonAsync<MessageFeedResponse>(
            $"/api/v1/conversations/{conversation.Id}/messages");
        Assert.Equal([keep.Id], feed!.Messages.Select(m => m.Id).ToArray());
    }

    [Fact]
    public async Task Moderator_SuspendingAMessageReport_HitsTheSenderNotTheReporter()
    {
        var (sender, senderName, senderEmail) = await RegisterClientWithEmail();
        var (recipient, recipientName) = await RegisterClient();
        var conversation = await StartConversation(sender, recipientName);
        var message = await SendMessage(sender, conversation.Id, "abusive");
        Assert.Equal(HttpStatusCode.NoContent, (await Report(recipient, conversation.Id, message.Id)).StatusCode);

        var moderator = await ModeratorClient();
        var queue = await moderator.GetFromJsonAsync<ReportQueueResponse>("/api/v1/admin/reports");
        var entry = Assert.Single(queue!.Reports, r => r.TargetId == message.Id);

        var resolved = await moderator.PostAsJsonAsync(
            $"/api/v1/admin/reports/{entry.Id}/resolve",
            new { action = "TemporarySuspension", reason = "abusive DM", suspensionDays = 7 });
        Assert.Equal(HttpStatusCode.NoContent, resolved.StatusCode);

        // The action follows the message back to whoever wrote it. The obvious bug this guards
        // is "message target isn't a user target, so the ladder 404s or hits the reporter".
        var login = await _scenarios.LoginAsync(factory.CreateClient(), senderEmail);
        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);

        var account = await moderator.GetFromJsonAsync<UserAccountResponse>($"/api/v1/admin/users/{senderName}");
        Assert.NotNull(account!.SuspendedUntil);
        Assert.Contains(
            account.ModerationHistory,
            e => e.Action == "TemporarySuspension" && e.Reason == "abusive DM");
    }
}