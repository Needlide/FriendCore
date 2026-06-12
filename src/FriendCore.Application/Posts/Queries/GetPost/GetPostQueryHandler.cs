using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Application.Posts.Common;
using FriendCore.Domain.Posts;
using MediatR;

namespace FriendCore.Application.Posts.Queries.GetPost;

internal sealed class GetPostQueryHandler(
    ICurrentUserService currentUserService,
    IPostRepository postRepository,
    PostResponseAssembler responseAssembler)
    : IRequestHandler<GetPostQuery, ErrorOr<PostResponse>>
{
    public async Task<ErrorOr<PostResponse>> Handle(GetPostQuery request, CancellationToken cancellationToken)
    {
        var post = await postRepository.GetByIdAsync(request.PostId, cancellationToken);
        if (post is null)
            return PostErrors.NotFound;

        // An empty assembly means the author is unresolvable (block-hidden or gone) — same 404.
        var items = await responseAssembler.AssembleAsync([post], currentUserService.UserId, cancellationToken);
        return items.Count == 0 ? PostErrors.NotFound : items[0];
    }
}
