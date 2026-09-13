using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Tags.Queries.GetMyProfileTags;
using FriendCore.Domain.Tags;
using FriendCore.Domain.Users;
using FriendCore.UnitTests.Users;
using NSubstitute;

namespace FriendCore.UnitTests.Tags;

public class GetMyProfileTagsQueryHandlerTests
{
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly ITagRepository _tagRepository = Substitute.For<ITagRepository>();

    private GetMyProfileTagsQueryHandler CreateHandler() =>
        new(_currentUserService, _userRepository, _tagRepository);

    [Fact]
    public async Task Handle_ReturnsUnauthorized_WhenNoCurrentUser()
    {
        _currentUserService.UserId.Returns((Ulid?)null);

        var result = await CreateHandler().Handle(new GetMyProfileTagsQuery(), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Common.Unauthorized", result.FirstError.Code);
    }

    [Fact]
    public async Task Handle_ReturnsEmpty_WhenNoTags()
    {
        var user = UserTestFactory.Create();
        _currentUserService.UserId.Returns(user.Id);
        _userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var result = await CreateHandler().Handle(new GetMyProfileTagsQuery(), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Empty(result.Value.Tags);
        await _tagRepository.DidNotReceiveWithAnyArgs().GetByIdsAsync(default!, default);
    }

    [Fact]
    public async Task Handle_ReturnsTags_WithTierAndVisibility()
    {
        var user = UserTestFactory.Create();
        var tag = Tag.Create("Synths", "synths", user.Id, UserTestFactory.DefaultCreatedAt);
        user.AddProfileTag(tag.Id, TagTier.Primary, true, UserTestFactory.DefaultCreatedAt);
        _currentUserService.UserId.Returns(user.Id);
        _userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        _tagRepository.GetByIdsAsync(Arg.Any<IReadOnlyCollection<Ulid>>(), Arg.Any<CancellationToken>())
            .Returns([tag]);

        var result = await CreateHandler().Handle(new GetMyProfileTagsQuery(), CancellationToken.None);

        Assert.False(result.IsError);
        var item = Assert.Single(result.Value.Tags);
        Assert.Equal("Synths", item.Name);
        Assert.Equal("synths", item.Slug);
        Assert.Equal("Primary", item.Tier);
        Assert.True(item.IsPublic);
    }
}
