using System.Net.Http.Json;
using FriendCore.Application.Common.Models;
using FriendCore.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace FriendCore.IntegrationTests.Realtime;

/// <summary>
///     End-to-end smoke test of the SignalR layer (§11.1): a real HubConnection authenticates
///     over the access_token query string, IUserIdProvider maps it to the user, and an HTTP
///     action on another account pushes through IRealtimeNotifier to the connected client.
///     Transport is forced to long-polling — TestServer-friendly, and the auth/push code under
///     test is transport-agnostic (production uses WebSockets).
/// </summary>
public class RealtimeHubTests(FriendCoreApiFactory factory) : IClassFixture<FriendCoreApiFactory>
{
    private static readonly TimeSpan PushTimeout = TimeSpan.FromSeconds(15);

    private async Task<(string Token, string Username)> RegisterAndLogin()
    {
        factory.Clock.Reset();
        var client = factory.CreateClient();
        var email = AuthScenarios.UniqueEmail();
        var username = AuthScenarios.NewUsername();
        await AuthScenarios.Register(client, email, username);
        var login = await new AuthScenarios(factory).LoginAsync(client, email);
        var body = await login.Content.ReadFromJsonAsync<AuthResponse>();
        return (body!.AccessToken, username);
    }

    private HubConnection BuildConnection(string hubPath, string token)
    {
        return new HubConnectionBuilder()
            .WithUrl($"http://localhost{hubPath}", options =>
            {
                // Route the connection through the in-memory TestServer, and supply the JWT —
                // the client sends it as a bearer header on negotiate and as ?access_token= on
                // the transport requests (which our OnMessageReceived hook reads).
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();
    }

    [Fact]
    public async Task NotificationHub_PushesNewFollowerToRecipient()
    {
        var (recipientToken, recipientName) = await RegisterAndLogin();
        var (actor, _) = await new AuthScenarios(factory).RegisterVerifiedClientAsync();

        var connection = BuildConnection("/hubs/notifications", recipientToken);
        var received = new TaskCompletionSource<RealtimeNotification>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<RealtimeNotification>(
            "NotificationReceived", payload => received.TrySetResult(payload));

        await connection.StartAsync();
        try
        {
            await actor.PostAsync($"/api/v1/users/{recipientName}/follow", content: null);

            var notification = await received.Task.WaitAsync(PushTimeout);
            Assert.Equal("NewFollower", notification.Type);
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }

    [Fact]
    public async Task MessagingHub_PushesIncomingMessageToRecipient()
    {
        var (recipientToken, recipientName) = await RegisterAndLogin();
        var (sender, _) = await new AuthScenarios(factory).RegisterVerifiedClientAsync();

        var connection = BuildConnection("/hubs/messaging", recipientToken);
        var received = new TaskCompletionSource<MessageResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<MessageResponse>("MessageReceived", message => received.TrySetResult(message));

        await connection.StartAsync();
        try
        {
            var start = await sender.PostAsJsonAsync("/api/v1/conversations", new { username = recipientName });
            var conversation = await start.Content.ReadFromJsonAsync<ConversationResponse>();
            await sender.PostAsJsonAsync(
                $"/api/v1/conversations/{conversation!.Id}/messages", new { content = "live ping" });

            var message = await received.Task.WaitAsync(PushTimeout);
            Assert.Equal("live ping", message.Content);
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }

    [Fact]
    public async Task Hub_RejectsConnection_WithoutToken()
    {
        var connection = new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/notifications", options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();

        // No access token → the hub's [Authorize] rejects the handshake.
        await Assert.ThrowsAnyAsync<Exception>(() => connection.StartAsync());
        await connection.DisposeAsync();
    }
}
