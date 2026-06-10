using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Tags.Commands.UpdateProfileTags;
using FriendCore.Application.Tags.Services;
using FriendCore.Domain.Tags;
using FriendCore.Domain.Users;
using FriendCore.UnitTests.Users;
using NSubstitute;

namespace FriendCore.UnitTests.Tags;

public class UpdateProfileTagsCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 2, 9, 0, 0, TimeSpan.Zero);
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly ITagRepository _tagRepository = Substitute.For<ITagRepository>();
    private readonly User _user = UserTestFactory.Create();

    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();

    public UpdateProfileTagsCommandHandlerTests()
    {
        _currentUserService.UserId.Returns(_user.Id);
        _dateTimeProvider.UtcNow.Returns(Now);
        _userRepository.GetByIdAsync(_user.Id, Arg.Any<CancellationToken>()).Returns(_user);
        _tagRepository.GetBySlugAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((Tag?)null);
        _tagRepository.GetByIdsAsync(Arg.Any<IReadOnlyCollection<Ulid>>(), Arg.Any<CancellationToken>())
            .Returns([]);
    }

    private UpdateProfileTagsCommandHandler CreateHandler()
    {
        return new UpdateProfileTagsCommandHandler(_userRepository, _tagRepository, new TagNormalizationService(),
            _currentUserService,
            _dateTimeProvider);
    }

    private static UpdateProfileTagsCommand Command(params ProfileTagInput[] tags)
    {
        return new UpdateProfileTagsCommand(tags);
    }

    [Fact]
    public async Task Handle_ReturnsUnauthorized_WhenNoCurrentUser()
    {
        _currentUserService.UserId.Returns((Ulid?)null);

        var result = await CreateHandler().Handle(
            Command(new ProfileTagInput("Photography", TagTier.Primary, true)),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Common.Unauthorized", result.FirstError.Code);
    }

    [Fact]
    public async Task Handle_ReturnsNotFound_WhenUserMissing()
    {
        _userRepository.GetByIdAsync(_user.Id, Arg.Any<CancellationToken>()).Returns((User?)null);

        var result = await CreateHandler().Handle(
            Command(new ProfileTagInput("Photography", TagTier.Primary, true)),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("User.NotFound", result.FirstError.Code);
    }

    [Fact]
    public async Task Handle_CreatesNewTagsAttachesAndReturnsThem()
    {
        var result = await CreateHandler().Handle(
            Command(
                new ProfileTagInput("Photography", TagTier.Primary, true),
                new ProfileTagInput("Indie Game Dev", TagTier.Secondary, false)),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(2, result.Value.Tags.Count);
        Assert.Equal(2, _user.ProfileTags.Count);
        await _tagRepository.Received(1)
            .AddRangeAsync(Arg.Is<IEnumerable<Tag>>(tags => tags.Count() == 2), Arg.Any<CancellationToken>());
        await _userRepository.Received(1).UpdateAsync(_user, Arg.Any<CancellationToken>());
        Assert.Contains(result.Value.Tags, t => t.Slug == "photography" && t.Tier == "Primary" && t.IsPublic);
        Assert.Contains(result.Value.Tags, t => t.Slug == "indie-game-dev" && t.Tier == "Secondary" && !t.IsPublic);
    }

    [Fact]
    public async Task Handle_ReusesExistingTagBySlug_AndIncrementsCount()
    {
        var existing = Tag.Create("Photography", "photography", Ulid.NewUlid(), Now);
        _tagRepository.GetBySlugResolvingAliasAsync("photography", Arg.Any<CancellationToken>()).Returns(existing);

        var result = await CreateHandler().Handle(
            Command(new ProfileTagInput("photography", TagTier.Primary, true)),
            CancellationToken.None);

        Assert.False(result.IsError);
        await _tagRepository.DidNotReceive().AddRangeAsync(Arg.Any<IEnumerable<Tag>>(), Arg.Any<CancellationToken>());
        Assert.Equal(1, existing.ProfileCount);
        Assert.Equal(existing.Id.ToString(), Assert.Single(result.Value.Tags).TagId);
    }

    [Fact]
    public async Task Handle_RejectsDuplicateSlugInRequest()
    {
        var result = await CreateHandler().Handle(
            Command(
                new ProfileTagInput("Photography", TagTier.Primary, true),
                new ProfileTagInput("  photography ", TagTier.Secondary, false)),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Tag.Duplicate", result.FirstError.Code);
    }

    [Fact]
    public async Task Handle_RejectsTooManyPrimaryTags()
    {
        var inputs = Enumerable.Range(0, TagLimits.MaxPrimaryProfileTags + 1)
            .Select(i => new ProfileTagInput($"tag number {i}", TagTier.Primary, true))
            .ToArray();

        var result = await CreateHandler().Handle(Command(inputs), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Tag.TooManyPrimary", result.FirstError.Code);
    }

    [Fact]
    public async Task Handle_RemovesDroppedTag_AndDecrementsItsCount()
    {
        var existingTag = Tag.Create("Photography", "photography", Ulid.NewUlid(), Now);
        existingTag.RecordProfileAdded(Now);
        _user.AddProfileTag(existingTag.Id, TagTier.Primary, true, Now);
        _tagRepository.GetByIdsAsync(Arg.Any<IReadOnlyCollection<Ulid>>(), Arg.Any<CancellationToken>())
            .Returns([existingTag]);

        var result = await CreateHandler().Handle(Command(), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Empty(_user.ProfileTags);
        Assert.Empty(result.Value.Tags);
        Assert.Equal(0, existingTag.ProfileCount);
    }
}