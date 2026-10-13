using FriendCore.Domain.Messaging;

namespace FriendCore.UnitTests.Messaging;

public class MessagingDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 13, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreateDirect_BuildsTwoParticipants()
    {
        var a = Ulid.NewUlid();
        var b = Ulid.NewUlid();

        var result = Conversation.CreateDirect(a, b, Now);

        Assert.False(result.IsError);
        var conversation = result.Value;
        Assert.Equal(2, conversation.Participants.Count);
        Assert.True(conversation.HasParticipant(a));
        Assert.True(conversation.HasParticipant(b));
        Assert.Equal(Now, conversation.LastMessageAt);
        Assert.Equal(b, conversation.OtherParticipantId(a));
        Assert.Equal(a, conversation.OtherParticipantId(b));
    }

    [Fact]
    public void CreateDirect_WithSelf_ReturnsError()
    {
        var a = Ulid.NewUlid();

        var result = Conversation.CreateDirect(a, a, Now);

        Assert.True(result.IsError);
        Assert.Equal("Messaging.CannotMessageSelf", result.FirstError.Code);
    }

    [Fact]
    public void MarkRead_AdvancesParticipantMarker_Monotonically()
    {
        var a = Ulid.NewUlid();
        var b = Ulid.NewUlid();
        var conversation = Conversation.CreateDirect(a, b, Now).Value;

        Assert.True(conversation.MarkRead(a, Now.AddMinutes(5)));
        Assert.Equal(Now.AddMinutes(5), conversation.LastReadAtFor(a));

        // A stale receipt cannot roll the marker backwards.
        conversation.MarkRead(a, Now.AddMinutes(1));
        Assert.Equal(Now.AddMinutes(5), conversation.LastReadAtFor(a));

        // Unknown participant is a no-op.
        Assert.False(conversation.MarkRead(Ulid.NewUlid(), Now));
    }

    [Fact]
    public void RecordMessage_UpdatesLastMessageAt()
    {
        var conversation = Conversation.CreateDirect(Ulid.NewUlid(), Ulid.NewUlid(), Now).Value;

        conversation.RecordMessage(Now.AddHours(1));

        Assert.Equal(Now.AddHours(1), conversation.LastMessageAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Message_Create_WithoutTextOrAttachment_ReturnsError(string? content)
    {
        var result = Message.Create(Ulid.NewUlid(), Ulid.NewUlid(), content, Now);

        Assert.True(result.IsError);
        Assert.Equal("Messaging.MessageEmpty", result.FirstError.Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Message_Create_WithAttachmentAndNoText_Succeeds(string? content)
    {
        var mediaId = Ulid.NewUlid();

        var result = Message.Create(Ulid.NewUlid(), Ulid.NewUlid(), content, Now, mediaId);

        Assert.False(result.IsError);
        // Whitespace next to an attachment is noise, not a caption.
        Assert.Null(result.Value.Content);
        Assert.Equal(mediaId, result.Value.MediaItemId);
    }

    [Fact]
    public void Message_Create_WithAttachmentAndText_KeepsBoth()
    {
        var mediaId = Ulid.NewUlid();

        var result = Message.Create(Ulid.NewUlid(), Ulid.NewUlid(), "look at this", Now, mediaId);

        Assert.False(result.IsError);
        Assert.Equal("look at this", result.Value.Content);
        Assert.Equal(mediaId, result.Value.MediaItemId);
    }

    [Fact]
    public void Message_Create_TextOnly_NullsTheAttachment()
    {
        var result = Message.Create(Ulid.NewUlid(), Ulid.NewUlid(), "hi", Now);

        Assert.False(result.IsError);
        Assert.Null(result.Value.MediaItemId);
    }

    [Fact]
    public void Message_Create_RejectsTooLongContent()
    {
        var content = new string('x', Message.MaxContentLength + 1);

        var result = Message.Create(Ulid.NewUlid(), Ulid.NewUlid(), content, Now);

        Assert.True(result.IsError);
        Assert.Equal("Messaging.MessageTooLong", result.FirstError.Code);
    }

    [Fact]
    public void Message_Create_TrimsContent_AndStartsSentPlaintext()
    {
        var result = Message.Create(Ulid.NewUlid(), Ulid.NewUlid(), "  hi there  ", Now);

        Assert.False(result.IsError);
        Assert.Equal("hi there", result.Value.Content);
        Assert.Equal(MessageStatus.Sent, result.Value.Status);
        Assert.False(result.Value.IsEncrypted);
    }

    [Fact]
    public void Message_Edit_ReplacesTextAndStampsEditedAt()
    {
        var message = Message.Create(Ulid.NewUlid(), Ulid.NewUlid(), "original", Now).Value;

        var result = message.Edit("  corrected  ", Now.AddMinutes(5));

        Assert.False(result.IsError);
        Assert.Equal("corrected", message.Content);
        Assert.Equal(Now.AddMinutes(5), message.EditedAt);
    }

    [Fact]
    public void Message_Edit_KeepsTheFirstEditTime_OnRepeatedEdits()
    {
        var message = Message.Create(Ulid.NewUlid(), Ulid.NewUlid(), "original", Now).Value;

        message.Edit("first", Now.AddMinutes(5));
        message.Edit("second", Now.AddMinutes(9));

        // "edited" is a fact about the message; letting it drift would make the marker lie
        // about when the text actually last changed.
        Assert.Equal("second", message.Content);
        Assert.Equal(Now.AddMinutes(5), message.EditedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Message_Edit_RejectsEmptyText(string content)
    {
        var message = Message.Create(Ulid.NewUlid(), Ulid.NewUlid(), "original", Now).Value;

        var result = message.Edit(content, Now);

        Assert.Equal("Messaging.MessageEmpty", result.FirstError.Code);
        Assert.Equal("original", message.Content);
        Assert.Null(message.EditedAt);
    }

    [Fact]
    public void Message_Edit_RejectsTooLongText()
    {
        var message = Message.Create(Ulid.NewUlid(), Ulid.NewUlid(), "original", Now).Value;

        var result = message.Edit(new string('x', Message.MaxContentLength + 1), Now);

        Assert.Equal("Messaging.MessageTooLong", result.FirstError.Code);
    }

    [Fact]
    public void Message_Edit_RefusesAnAttachmentOnlyMessage()
    {
        var message = Message.Create(Ulid.NewUlid(), Ulid.NewUlid(), null, Now, Ulid.NewUlid()).Value;

        var result = message.Edit("a caption I never typed", Now);

        // Half-applying this would put words in the sender's mouth on a bubble they sent bare.
        Assert.Equal("Messaging.MessageNotEditable", result.FirstError.Code);
        Assert.Null(message.Content);
    }

    [Fact]
    public void Message_Edit_RefusesADeletedMessage()
    {
        var message = Message.Create(Ulid.NewUlid(), Ulid.NewUlid(), "original", Now).Value;
        message.MarkDeleted(Now);

        var result = message.Edit("resurrect me", Now.AddMinutes(1));

        // The row is invisible to every query, so "success" here would be a lie.
        Assert.Equal("Messaging.MessageNotFound", result.FirstError.Code);
    }

    [Fact]
    public void Message_MarkDeleted_ScrubsTheText()
    {
        var message = Message.Create(Ulid.NewUlid(), Ulid.NewUlid(), "something I regret", Now).Value;

        message.MarkDeleted(Now);

        Assert.Equal(Now, message.DeletedAt);
        // The words must not survive in the row: the spec never fixes a retention window for
        // soft-deleted content, so leaving the text for the sweeper is not a privacy guarantee.
        Assert.Null(message.Content);
    }

    [Fact]
    public void Message_MarkDeleted_IsIdempotent()
    {
        var message = Message.Create(Ulid.NewUlid(), Ulid.NewUlid(), "original", Now).Value;

        message.MarkDeleted(Now);
        message.MarkDeleted(Now.AddMinutes(5));

        Assert.Equal(Now, message.DeletedAt);
    }

    [Fact]
    public void Edit_StartsAtVersionZero_SoAnUneditedMessagePresentsZero()
    {
        // Not a cosmetic default: the client sends this back verbatim, so the value a fresh
        // message carries is part of the contract.
        Assert.Equal(0, Message.Create(Ulid.NewUlid(), Ulid.NewUlid(), "hello", Now).Value.Version);
    }

    [Fact]
    public void Edit_AcceptsTheCurrentVersion_AndLeavesTheBumpToPersistence()
    {
        var message = Message.Create(Ulid.NewUlid(), Ulid.NewUlid(), "hello", Now).Value;

        var result = message.Edit("hello again", Now.AddMinutes(1), expectedVersion: 0);

        Assert.True(result.IsSuccess);
        // Edit does not bump it: the compare-and-set does, in SQL, atomically with the write.
        // Bumping here too would let a rejected edit advance the version out from under the
        // client that still holds the old one.
        Assert.Equal(0, message.Version);

        message.MarkEditCommitted();
        Assert.Equal(1, message.Version);
    }

    [Fact]
    public void Edit_RefusesAStaleVersion_AndChangesNothing()
    {
        var message = Message.Create(Ulid.NewUlid(), Ulid.NewUlid(), "hello", Now).Value;
        message.MarkEditCommitted(); // another device already saved

        var result = message.Edit("my reword", Now.AddMinutes(1), expectedVersion: 0);

        Assert.Equal("Messaging.MessageChanged", result.FirstError.Code);
        // A refused edit must leave the message exactly as it was, or a conflict would still
        // cost the user their wording.
        Assert.Equal("hello", message.Content);
        Assert.Null(message.EditedAt);
        Assert.Equal(1, message.Version);
    }

    [Fact]
    public void Edit_SkipsTheCheck_WhenNoVersionIsPresented()
    {
        var message = Message.Create(Ulid.NewUlid(), Ulid.NewUlid(), "hello", Now).Value;
        message.MarkEditCommitted();

        // The domain honours a null for a caller with no version to present; the API layer is
        // what refuses to accept one, so no real client can reach this without one.
        Assert.True(message.Edit("hello again", Now.AddMinutes(1)).IsSuccess);
    }

    [Fact]
    public void Edit_ReportsTheConflict_AfterTheTextChecks()
    {
        var message = Message.Create(Ulid.NewUlid(), Ulid.NewUlid(), "hello", Now).Value;
        message.MarkEditCommitted();

        // A caller working from a stale view should be told about the conflict, not handed a
        // validation complaint about text they never actually sent.
        var tooLong = message.Edit(new string('x', Message.MaxContentLength + 1), Now, 0);
        Assert.Equal("Messaging.MessageTooLong", tooLong.FirstError.Code);

        var blank = message.Edit("   ", Now, 0);
        Assert.Equal("Messaging.MessageEmpty", blank.FirstError.Code);

        var fine = message.Edit("a perfectly good reword", Now, 0);
        Assert.Equal("Messaging.MessageChanged", fine.FirstError.Code);
    }

    [Fact]
    public void Edit_RefusesADeletedMessage_BeforeConsideringTheVersion()
    {
        var message = Message.Create(Ulid.NewUlid(), Ulid.NewUlid(), "hello", Now).Value;
        message.MarkDeleted(Now);

        // A deleted message is a 404 whatever the version says — an edit must never resurrect
        // one, and "not found" is the more useful answer than "conflict".
        Assert.Equal("Messaging.MessageNotFound", message.Edit("back?", Now, 0).FirstError.Code);
    }
}
