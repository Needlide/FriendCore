using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Options;
using FriendCore.Application.Tags.Commands.ArchiveStaleTags;
using FriendCore.Domain.Tags;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace FriendCore.UnitTests.Tags;

public class ArchiveStaleTagsCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 7, 0, 0, 0, TimeSpan.Zero);
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();

    private readonly ITagRepository _tagRepository = Substitute.For<ITagRepository>();

    public ArchiveStaleTagsCommandHandlerTests()
    {
        _dateTimeProvider.UtcNow.Returns(Now);
    }

    private ArchiveStaleTagsCommandHandler CreateHandler()
    {
        return new ArchiveStaleTagsCommandHandler(_tagRepository, _dateTimeProvider,
            Options.Create(new TagMaintenanceOptions { ArchiveBatchSize = 500 }));
    }

    [Fact]
    public async Task Handle_ReturnsZero_WhenNothingStale()
    {
        _tagRepository
            .GetArchivableAsync(Arg.Any<int>(), Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
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
        _tagRepository.GetArchivableAsync(3, Now.AddDays(-180), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([stale]);

        var result = await CreateHandler().Handle(new ArchiveStaleTagsCommand(), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(1, result.Value.ArchivedCount);
        Assert.Equal(TagStatus.Archived, stale.Status);
        await _tagRepository.Received(1).UpdateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DrainsAcrossBatches_UntilShortBatchReturned()
    {
        // First call returns a full batch (signals more may remain), second returns a short batch (terminates).
        var first = CreateStaleBatch(500);
        var second = CreateStaleBatch(120);
        _tagRepository
            .GetArchivableAsync(3, Now.AddDays(-180), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(first, second);

        var result = await CreateHandler().Handle(new ArchiveStaleTagsCommand(), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(620, result.Value.ArchivedCount);
        Assert.All(first, tag => Assert.Equal(TagStatus.Archived, tag.Status));
        Assert.All(second, tag => Assert.Equal(TagStatus.Archived, tag.Status));
        await _tagRepository.Received(2).UpdateAsync(Arg.Any<CancellationToken>());
    }

    private static List<Tag> CreateStaleBatch(int count)
    {
        var batch = new List<Tag>(count);
        for (var i = 0; i < count; i++)
        {
            var tag = Tag.Create($"Old Tag {i}", $"old-tag-{i}", Ulid.NewUlid(), Now.AddYears(-1));
            tag.Approve();
            batch.Add(tag);
        }

        return batch;
    }
}