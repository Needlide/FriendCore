using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Tags.Commands.ArchiveStaleTags;
using FriendCore.Domain.Tags;
using NSubstitute;

namespace FriendCore.UnitTests.Tags;

public class ArchiveStaleTagsCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 7, 0, 0, 0, TimeSpan.Zero);

    private readonly ITagRepository _tagRepository = Substitute.For<ITagRepository>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();

    public ArchiveStaleTagsCommandHandlerTests()
    {
        _dateTimeProvider.UtcNow.Returns(Now);
    }

    private ArchiveStaleTagsCommandHandler CreateHandler() => new(_tagRepository, _dateTimeProvider);

    [Fact]
    public async Task Handle_ReturnsZero_WhenNothingStale()
    {
        _tagRepository.GetArchivableAsync(Arg.Any<int>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns([]);

        var result = await CreateHandler().Handle(new ArchiveStaleTagsCommand(), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(0, result.Value.ArchivedCount);
        await _tagRepository.DidNotReceive().UpdateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ArchivesStaleTags_WithCutoff180DaysBeforeNow()
    {
        var stale = Tag.Create("Old Tag", "old-tag", Ulid.NewUlid(), Now.AddYears(-1));
        stale.Approve();
        _tagRepository.GetArchivableAsync(3, Now.AddDays(-180), Arg.Any<CancellationToken>())
            .Returns([stale]);

        var result = await CreateHandler().Handle(new ArchiveStaleTagsCommand(), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(1, result.Value.ArchivedCount);
        Assert.Equal(TagStatus.Archived, stale.Status);
        await _tagRepository.Received(1).UpdateAsync(Arg.Any<CancellationToken>());
    }
}
