using System.Net;
using System.Net.Http.Json;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Messaging;
using FriendCore.Infrastructure.Persistence;
using FriendCore.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Microsoft.Extensions.DependencyInjection;

namespace FriendCore.IntegrationTests.Messaging;

/// <summary>
///     One direct conversation per pair of people.
/// </summary>
/// <remarks>
///     Previously enforced only by a find-then-insert with no constraint behind it, which is fine
///     right up until two people press "Message" at the same moment. Both then find nothing, both
///     insert, and the pair ends up with two threads that never merge.
/// </remarks>
public class DirectConversationUniquenessFlowTests(FriendCoreApiFactory factory)
    : IClassFixture<FriendCoreApiFactory>
{
    private Task<(HttpClient Client, string Username)> RegisterClient() =>
        new AuthScenarios(factory).RegisterVerifiedClientAsync();

    private static async Task<ConversationResponse> Start(HttpClient client, string username)
    {
        var response = await client.PostAsJsonAsync("/api/v1/conversations", new { username });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ConversationResponse>())!;
    }

    private async Task<string> DirectKeyOfAsync(string conversationId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Conversations
            .Where(conversation => conversation.Id == Ulid.Parse(conversationId))
            .Select(conversation => conversation.DirectKey)
            .SingleAsync();
    }

    private async Task<int> CountWithDirectKeyAsync(string directKey)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Conversations.CountAsync(conversation => conversation.DirectKey == directKey);
    }

    [Fact]
    public async Task Starting_Twice_Returns_The_Same_Conversation()
    {
        var (alice, _) = await RegisterClient();
        var (_, bobName) = await RegisterClient();

        var first = await Start(alice, bobName);
        var second = await Start(alice, bobName);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, await CountWithDirectKeyAsync(await DirectKeyOfAsync(first.Id)));
    }

    [Fact]
    public async Task The_Reverse_Direction_Finds_The_Same_Conversation()
    {
        // (A,B) and (B,A) must not be two rows. The key is sorted precisely so whichever side
        // pressed the button first cannot decide who ends up with a duplicate.
        var (alice, aliceName) = await RegisterClient();
        var (bob, bobName) = await RegisterClient();

        var fromAlice = await Start(alice, bobName);
        var fromBob = await Start(bob, aliceName);

        Assert.Equal(fromAlice.Id, fromBob.Id);
        Assert.Equal(1, await CountWithDirectKeyAsync(await DirectKeyOfAsync(fromAlice.Id)));
    }

    [Fact]
    public async Task The_Database_Rejects_A_Second_Direct_Conversation_For_The_Same_Pair()
    {
        // The application check is not the guarantee — this is. If a second code path ever inserts
        // a conversation without going through find-or-create, the index is what stops it.
        var (alice, _) = await RegisterClient();
        var (_, bobName) = await RegisterClient();

        var created = await Start(alice, bobName);
        var directKey = await DirectKeyOfAsync(created.Id);
        Assert.NotNull(directKey);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Asserts on the NAMED constraint rather than "some DbUpdateException". ExecuteSqlRaw
        // surfaces the Postgres error unwrapped, so this is also the stronger claim: not merely
        // that the insert failed, but that it failed on the unique index and nothing else.
        var violation = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO conversations (id, last_message_at, created_at, direct_key)
                VALUES ({0}, now(), now(), {1})
                """,
                // As strings, not Ulids: ids are stored as text and ExecuteSqlRaw has no value
                // converter for Ulid, so passing one fails before it ever reaches Postgres.
                Ulid.NewUlid().ToString(), directKey!);
        });

        Assert.Equal(PostgresErrorCodes.UniqueViolation, violation.SqlState);
        Assert.Contains("ix_conversations_direct_key", violation.MessageText);
    }

    [Fact]
    public async Task Different_Pairs_Are_Unaffected()
    {
        // The index must not turn into a global "one conversation" rule.
        var (alice, _) = await RegisterClient();
        var (_, bobName) = await RegisterClient();
        var (_, carolName) = await RegisterClient();

        var withBob = await Start(alice, bobName);
        var withCarol = await Start(alice, carolName);

        Assert.NotEqual(withBob.Id, withCarol.Id);
    }

    [Fact]
    public async Task The_Key_Does_Not_Depend_On_Argument_Order()
    {
        // Pure domain check, and the reason the uniqueness holds at all.
        var a = Ulid.NewUlid();
        var b = Ulid.NewUlid();

        Assert.Equal(Conversation.DirectKeyFor(a, b), Conversation.DirectKeyFor(b, a));
    }
}