using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Tags.Commands.MergeTags;
using FriendCore.Domain.Tags;
using FriendCore.Domain.Users;
using FriendCore.UnitTests.Users;
using NSubstitute;

namespace FriendCore.UnitTests.Tags;

public class MergeTagsCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 7, 9, 0, 0, TimeSpan.Zero);

    private readonly ITagRepository _tagRepository = Substitute.For<ITagRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();

    public MergeTagsCommandHandlerTests()
    {
        _dateTimeProvider.UtcNow.Returns(Now);
        _userRepository.GetUsersWithProfileTagAsync(Arg.Any<Ulid>(), Arg.Any<CancellationToken>()).Returns([]);
    }

    private MergeTagsCommandHandler CreateHandler() => new(_tagRepository, _userRepository, _dateTimeProvider);

    private static Tag ActiveTag(string name, string slug)
    {
        var tag = Tag.Create(name, slug, Ulid.NewUlid(), Now);
        tag.Approve();
        return tag;
    }

    private void Register(params Tag[] tags)
    {
        foreach (var tag in tags)
            _tagRepository.GetByIdAsync(tag.Id, Arg.Any<CancellationToken>()).Returns(tag);
    }

    [Fact]
    public async Task Handle_RejectsMergingIntoSelf()
    {
        var id = Ulid.NewUlid();

        var result = await CreateHandler().Handle(new MergeTagsCommand(id, id), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Tag.CannotMergeIntoSelf", result.FirstError.Code);
    }

    [Fact]
    public async Task Handle_ReturnsNotFound_WhenSourceMissing()
    {
        var target = ActiveTag("Target", "target");
        Register(target);
        _tagRepository.GetByIdAsync(Arg.Is<Ulid>(x => x != target.Id), Arg.Any<CancellationToken>()).Returns((Tag?)null);

        var result = await CreateHandler().Handle(new MergeTagsCommand(Ulid.NewUlid(), target.Id), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Tag.NotFound", result.FirstError.Code);
    }

    [Fact]
    public async Task Handle_RejectsNonActiveTarget()
    {
        var source = ActiveTag("Source", "source");
        var target = Tag.Create("Target", "target", Ulid.NewUlid(), Now); // PendingReview
        Register(source, target);

        var result = await CreateHandler().Handle(new MergeTagsCommand(source.Id, target.Id), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Tag.InvalidMergeTarget", result.FirstError.Code);
    }

    [Fact]
    public async Task Handle_RejectsAlreadyMergedSource()
    {
        var source = ActiveTag("Source", "source");
        var target = ActiveTag("Target", "target");
        source.MergeInto(Ulid.NewUlid());
        Register(source, target);

        var result = await CreateHandler().Handle(new MergeTagsCommand(source.Id, target.Id), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Tag.AlreadyMerged", result.FirstError.Code);
    }

    [Fact]
    public async Task Handle_MarksSourceMergedAndReturnsTarget()
    {
        var source = ActiveTag("Source", "source");
        var target = ActiveTag("Target", "target");
        Register(source, target);

        var result = await CreateHandler().Handle(new MergeTagsCommand(source.Id, target.Id), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(target.Id.ToString(), result.Value.Id);
        Assert.Equal(TagStatus.Merged, source.Status);
        Assert.Equal(target.Id, source.MergedIntoId);
        await _tagRepository.Received(1).UpdateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_RepointsUsersWithoutTarget_AndIncrementsTargetCount()
    {
        var source = ActiveTag("Source", "source");
        var target = ActiveTag("Target", "target");
        Register(source, target);

        var user = UserTestFactory.Create();
        user.AddProfileTag(source.Id, TagTier.Primary, isPublic: true, Now);
        _userRepository.GetUsersWithProfileTagAsync(source.Id, Arg.Any<CancellationToken>()).Returns([user]);

        await CreateHandler().Handle(new MergeTagsCommand(source.Id, target.Id), CancellationToken.None);

        Assert.DoesNotContain(user.ProfileTags, t => t.TagId == source.Id);
        var repointed = Assert.Single(user.ProfileTags);
        Assert.Equal(target.Id, repointed.TagId);
        Assert.Equal(TagTier.Primary, repointed.Tier);
        Assert.Equal(1, target.ProfileCount);
    }

    [Fact]
    public async Task Handle_DedupsUserWithBothTags_KeepingTarget()
    {
        var source = ActiveTag("Source", "source");
        var target = ActiveTag("Target", "target");
        Register(source, target);

        var user = UserTestFactory.Create();
        user.AddProfileTag(target.Id, TagTier.Secondary, isPublic: false, Now);
        user.AddProfileTag(source.Id, TagTier.Primary, isPublic: true, Now);
        _userRepository.GetUsersWithProfileTagAsync(source.Id, Arg.Any<CancellationToken>()).Returns([user]);

        await CreateHandler().Handle(new MergeTagsCommand(source.Id, target.Id), CancellationToken.None);

        var kept = Assert.Single(user.ProfileTags);
        Assert.Equal(target.Id, kept.TagId);
        Assert.Equal(TagTier.Secondary, kept.Tier);
        Assert.False(kept.IsPublic);
        Assert.Equal(0, target.ProfileCount);
    }
}
