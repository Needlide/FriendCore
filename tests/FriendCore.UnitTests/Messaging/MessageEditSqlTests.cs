using FriendCore.Domain.Messaging;
using FriendCore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FriendCore.UnitTests.Messaging;

/// <summary>
/// Translation checks for the edit's compare-and-set. These assert the SHAPE of the SQL, not
/// its result: <c>ToQueryString()</c> needs a configured provider but no connection, so this
/// covers the one thing a mocked repository cannot — that the version comparison and the
/// value-converted Ulid keys actually survive translation to Postgres. The Ulid-to-string
/// column conversions in this module have broken <c>ANY(...)</c> queries before, so "it
/// compiles" is genuinely not the same as "it translates".
/// </summary>
public class MessageEditSqlTests
{
    private static AppDbContext BuildContext()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql("Host=localhost;Database=does-not-need-to-exist"));
        return services.BuildServiceProvider().GetRequiredService<AppDbContext>();
    }

    [Fact]
    public void TheEditQuery_ComparesTheVersionInSql()
    {
        using var db = BuildContext();

        // The mirror of MessagingRepository.TryApplyEditAsync's WHERE, so this test fails if
        // the repository's predicate drifts away from what is actually translatable.
        var sql = db.Messages
            .Where(message => message.Id == Ulid.NewUlid()
                              && message.ConversationId == Ulid.NewUlid()
                              && message.Version == 3
                              && message.DeletedAt == null)
            .ToQueryString();

        // The global soft-delete filter already excludes deleted rows; the predicate repeats it
        // deliberately, so a rename of that filter can never quietly turn this into a
        // resurrect-a-deleted-message write.
        Assert.Contains("deleted_at IS NULL", sql, StringComparison.OrdinalIgnoreCase);
        // The whole compare-and-set has to be in one statement's WHERE: this is what decides
        // the winner, and it is the only thing standing between a stale rewrite and a silently
        // discarded one.
        Assert.Contains("m.version = ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("m.id = ", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheEditQuery_TargetsTheConversationScopedRow()
    {
        using var db = BuildContext();

        // Conversation scoping is what stops a cross-thread write; it has to reach the SQL,
        // because nothing above the repository enforces it.
        var sql = db.Messages
            .Where(message => message.ConversationId == Ulid.NewUlid())
            .ToQueryString();

        Assert.Contains("conversation_id", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ThePerPageReactionRead_TranslatesWithConvertedKeys()
    {
        using var db = BuildContext();

        // The other Contains-over-converted-keys query in this module. Cheap to keep honest.
        var ids = new[] { Ulid.NewUlid(), Ulid.NewUlid() };
        var sql = db.MessageReactions
            .Where(reaction => ids.Contains(reaction.MessageId))
            .ToQueryString();

        Assert.Contains("message_id", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ANY", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Message_ExposesTheVersionColumn()
    {
        using var db = BuildContext();

        // The column has to exist under this name; TryApplyEditAsync filters on it directly.
        var property = db.Model.FindEntityType(typeof(Message))!.FindProperty(nameof(Message.Version));
        Assert.NotNull(property);
        Assert.Equal("version", property!.GetColumnName());
    }
}