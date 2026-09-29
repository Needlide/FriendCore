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
}
