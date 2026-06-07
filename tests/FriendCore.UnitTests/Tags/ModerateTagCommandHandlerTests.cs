using FriendCore.Application.Tags.Commands.ModerateTag;
using FriendCore.Domain.Tags;
using NSubstitute;

namespace FriendCore.UnitTests.Tags;

public class ModerateTagCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 2, 9, 0, 0, TimeSpan.Zero);

    private readonly ITagRepository _tagRepository = Substitute.For<ITagRepository>();

    private ModerateTagCommandHandler CreateHandler() => new(_tagRepository);

    private Tag Pending(out Ulid id)
    {
        var tag = Tag.Create("Photography", "photography", Ulid.NewUlid(), Now);
        id = tag.Id;
        _tagRepository.GetByIdAsync(tag.Id, Arg.Any<CancellationToken>()).Returns(tag);
        return tag;
    }

    [Fact]
    public async Task Handle_ReturnsNotFound_WhenTagMissing()
    {
        _tagRepository.GetByIdAsync(Arg.Any<Ulid>(), Arg.Any<CancellationToken>()).Returns((Tag?)null);

        var result = await CreateHandler().Handle(
            new ModerateTagCommand(Ulid.NewUlid(), TagModerationAction.Approve),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Tag.NotFound", result.FirstError.Code);
    }

    [Fact]
    public async Task Handle_Approve_SetsActiveAndPersists()
    {
        var tag = Pending(out var id);

        var result = await CreateHandler().Handle(new ModerateTagCommand(id, TagModerationAction.Approve), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(TagStatus.Active, tag.Status);
        Assert.Equal(nameof(TagStatus.Active), result.Value.Status);
        await _tagRepository.Received(1).UpdateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Archive_SetsArchived()
    {
        var tag = Pending(out var id);

        var result = await CreateHandler().Handle(new ModerateTagCommand(id, TagModerationAction.Archive), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(TagStatus.Archived, tag.Status);
    }

    [Fact]
    public async Task Handle_Ban_SetsBanned()
    {
        var tag = Pending(out var id);

        var result = await CreateHandler().Handle(new ModerateTagCommand(id, TagModerationAction.Ban), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(TagStatus.Banned, tag.Status);
    }

    [Fact]
    public async Task Handle_Approve_OnBannedTag_ReturnsError()
    {
        var tag = Pending(out var id);
        tag.Ban();

        var result = await CreateHandler().Handle(new ModerateTagCommand(id, TagModerationAction.Approve), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Tag.Banned", result.FirstError.Code);
        await _tagRepository.DidNotReceive().UpdateAsync(Arg.Any<CancellationToken>());
    }
}
