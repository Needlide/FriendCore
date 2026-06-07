using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Tags.Commands.CreateOrGetTag;
using FriendCore.Application.Tags.Services;
using FriendCore.Domain.Tags;
using NSubstitute;

namespace FriendCore.UnitTests.Tags;

public class CreateOrGetTagCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 2, 9, 0, 0, TimeSpan.Zero);
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();

    private readonly ITagRepository _tagRepository = Substitute.For<ITagRepository>();
    private readonly Ulid _userId = Ulid.NewUlid();

    public CreateOrGetTagCommandHandlerTests()
    {
        _currentUserService.UserId.Returns(_userId);
        _dateTimeProvider.UtcNow.Returns(Now);
    }

    private CreateOrGetTagCommandHandler CreateHandler()
    {
        return new CreateOrGetTagCommandHandler(_tagRepository, new TagNormalizationService(), _currentUserService,
            _dateTimeProvider);
    }

    [Fact]
    public async Task Handle_ReturnsUnauthorized_WhenNoCurrentUser()
    {
        _currentUserService.UserId.Returns((Ulid?)null);

        var result = await CreateHandler().Handle(new CreateOrGetTagCommand("Photography"), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Common.Unauthorized", result.FirstError.Code);
    }

    [Fact]
    public async Task Handle_ReturnsValidationError_WhenNameNormalizesTooShort()
    {
        var result = await CreateHandler().Handle(new CreateOrGetTagCommand("!!!"), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.StartsWith("Tag.", result.FirstError.Code);
        await _tagRepository.DidNotReceive().AddAsync(Arg.Any<Tag>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ReturnsExistingTag_WithoutCreating()
    {
        var existing = Tag.Create("Photography", "photography", Ulid.NewUlid(), Now);
        _tagRepository.GetBySlugResolvingAliasAsync("photography", Arg.Any<CancellationToken>()).Returns(existing);

        var result = await CreateHandler().Handle(new CreateOrGetTagCommand("  Photography  "), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(existing.Id.ToString(), result.Value.Id);
        await _tagRepository.DidNotReceive().AddAsync(Arg.Any<Tag>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CreatesPendingReviewTag_WhenSlugNotFound()
    {
        _tagRepository.GetBySlugAsync("indie-game-dev", Arg.Any<CancellationToken>()).Returns((Tag?)null);
        Tag? added = null;
        await _tagRepository.AddAsync(Arg.Do<Tag>(t => added = t), Arg.Any<CancellationToken>());

        var result = await CreateHandler().Handle(new CreateOrGetTagCommand("Indie Game Dev"), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.NotNull(added);
        Assert.Equal("indie-game-dev", added!.Slug);
        Assert.Equal("Indie Game Dev", added.Name);
        Assert.Equal(TagStatus.PendingReview, added.Status);
        Assert.Equal(_userId, added.CreatedById);
        Assert.Equal(TagStatus.PendingReview.ToString(), result.Value.Status);
    }
}