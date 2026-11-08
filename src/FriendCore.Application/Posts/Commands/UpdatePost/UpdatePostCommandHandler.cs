using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Application.Posts.Common;
using FriendCore.Domain.Common;
using FriendCore.Domain.Posts;
using MediatR;

namespace FriendCore.Application.Posts.Commands.UpdatePost;

internal sealed class UpdatePostCommandHandler(
    ICurrentUserService currentUserService,
    IPostRepository postRepository,
    PostResponseAssembler responseAssembler,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<UpdatePostCommand, ErrorOr<PostResponse>>
{
    public async Task<ErrorOr<PostResponse>> Handle(UpdatePostCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return CommonErrors.Unauthorized;

        var post = await postRepository.GetByIdAsync(request.PostId, cancellationToken);
        if (post is null)
            return PostErrors.NotFound;

        if (post.AuthorId != userId)
            return PostErrors.NotAuthor;

        var edit = post.Edit(
            request.Content,
            (ContentSensitivity)request.Sensitivity,
            dateTimeProvider.UtcNow);
        if (edit.IsError)
            return edit.Errors;

        await postRepository.UpdateAsync(cancellationToken);

        var items = await responseAssembler.AssembleAsync([post], userId, cancellationToken);
        return items.Count == 0 ? PostErrors.NotFound : items[0];
    }
}
