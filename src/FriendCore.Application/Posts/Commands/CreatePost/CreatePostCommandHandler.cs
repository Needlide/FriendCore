using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Application.Posts.Common;
using FriendCore.Domain.Common;
using FriendCore.Domain.Media;
using FriendCore.Domain.Posts;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Posts.Commands.CreatePost;

internal sealed class CreatePostCommandHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository,
    IPostRepository postRepository,
    IMediaRepository mediaRepository,
    PostMediaResponseFactory mediaResponseFactory,
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

        // Parse is safe post-validation; the validator rejects malformed ids.
        var mediaIds = request.MediaIds.Select(Ulid.Parse).ToList();

        var mediaError = await ValidateMediaAsync(mediaIds, userId, cancellationToken);
        if (mediaError is { } error)
            return error;

        var postResult = Post.Create(
            userId, request.Content, MapSensitivity(request.Sensitivity), dateTimeProvider.UtcNow, mediaIds);
        if (postResult.IsError)
            return postResult.Errors;

        var post = postResult.Value;
        await postRepository.AddAsync(post, cancellationToken);

        var mediaByPost = await mediaResponseFactory.ForPostsAsync([post], cancellationToken);
        return PostResponse.Create(
            post, author, isLikedByCurrentUser: false, mediaByPost.GetValueOrDefault(post.Id, []));
    }

    // Ownership, readiness, and single-use checks need the repositories, so they live here rather
    // than in Post.Create. The unique index on post_media.media_item_id backstops the single-use
    // rule against concurrent attaches.
    private async Task<Error?> ValidateMediaAsync(
        IReadOnlyList<Ulid> mediaIds,
        Ulid userId,
        CancellationToken cancellationToken)
    {
        if (mediaIds.Count == 0)
            return null;

        var mediaItems = await mediaRepository.GetByIdsAsync(mediaIds, cancellationToken);

        if (mediaItems.Count != mediaIds.Distinct().Count())
            return MediaErrors.NotFound;

        if (mediaItems.Any(media => media.UploaderId != userId))
            return MediaErrors.NotOwner;

        if (mediaItems.Any(media => !media.IsReady))
            return MediaErrors.NotReady;

        var attached = await postRepository.GetAttachedMediaIdsAsync(mediaIds, cancellationToken);
        return attached.Count > 0 ? MediaErrors.AlreadyAttached : null;
    }

    // Out-of-range values are rejected by the validator's IsInEnum before the handler runs.
    private static ContentSensitivity MapSensitivity(PostSensitivity sensitivity) =>
        sensitivity switch
        {
            PostSensitivity.General => ContentSensitivity.General,
            PostSensitivity.Sensitive => ContentSensitivity.Sensitive,
            PostSensitivity.Explicit => ContentSensitivity.Explicit,
            _ => throw new ArgumentOutOfRangeException(nameof(sensitivity), sensitivity, null)
        };
}
