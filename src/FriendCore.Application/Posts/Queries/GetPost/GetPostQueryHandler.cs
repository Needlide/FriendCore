using ErrorOr;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Posts;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Posts.Queries.GetPost;

internal sealed class GetPostQueryHandler(
    IPostRepository postRepository,
    IUserRepository userRepository)
    : IRequestHandler<GetPostQuery, ErrorOr<PostResponse>>
{
    public async Task<ErrorOr<PostResponse>> Handle(GetPostQuery request, CancellationToken cancellationToken)
    {
        var post = await postRepository.GetByIdAsync(request.PostId, cancellationToken);
        if (post is null)
            return PostErrors.NotFound;

        var author = await userRepository.GetByIdAsync(post.AuthorId, cancellationToken);
        if (author is null)
            return PostErrors.NotFound;

        return PostResponse.Create(post, author, isLikedByCurrentUser: false);
    }
}
