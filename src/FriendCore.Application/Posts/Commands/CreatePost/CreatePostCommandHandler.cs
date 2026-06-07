using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Common;
using FriendCore.Domain.Posts;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Posts.Commands.CreatePost;

internal sealed class CreatePostCommandHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository,
    IPostRepository postRepository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<CreatePostCommand, ErrorOr<PostResponse>>
{
    public async Task<ErrorOr<PostResponse>> Handle(CreatePostCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return CommonErrors.Unauthorized;

        var author = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (author is null)
            return UserErrors.NotFound;

        var postResult = Post.Create(userId, request.Content, request.Sensitivity, dateTimeProvider.UtcNow);
        if (postResult.IsError)
            return postResult.Errors;

        var post = postResult.Value;
        await postRepository.AddAsync(post, cancellationToken);

        return PostResponse.Create(post, author, isLikedByCurrentUser: false);
    }
}
