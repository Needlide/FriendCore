using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Application.Tags.Services;
using FriendCore.Domain.Common;
using FriendCore.Domain.Media;
using FriendCore.Domain.Posts;
using FriendCore.Domain.Tags;
using FriendCore.Domain.Users;
using MediatR;

using FriendCore.Application.Common;
using FriendCore.Application.Common.Options;
using Microsoft.Extensions.Options;

namespace FriendCore.Application.Posts.Commands.CreatePost;

internal sealed class CreatePostCommandHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository,
    IPostRepository postRepository,
    IMediaRepository mediaRepository,
    ITagRepository tagRepository,
    ITagNormalizer normalizer,
    MediaResponseFactory mediaResponseFactory,
    IDateTimeProvider dateTimeProvider,
    IOptions<EmailOptions> emailOptions)
    : IRequestHandler<CreatePostCommand, ErrorOr<PostResponse>>
{
    public async Task<ErrorOr<PostResponse>> Handle(CreatePostCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return CommonErrors.Unauthorized;

        var author = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (author is null)
            return UserErrors.NotFound;

        // §13.7: email verification required before POSTING only — comments, likes, and follows
        // stay open to unverified accounts (deliberate comfort-first product decision; revisit
        // during closed testing). When RequireVerificationForPosting is false the gate is
        // skipped (useful when no email service is configured).
        if (emailOptions.Value.RequireVerificationForPosting && !author.IsVerified)
            return UserErrors.EmailNotVerified;

        // Fail before tag resolution persists anything: resolving creates missing tags, and an
        // over-cap post must not leave orphaned tag rows behind.
        if (request.Tags.Count > TagLimits.MaxPostTags)
            return PostErrors.TooManyTags;

        // Parse is safe post-validation; the validator rejects malformed ids.
        var mediaIds = request.MediaIds.Select(Ulid.Parse).ToList();

        var mediaError = await ValidateMediaAsync(mediaIds, userId, cancellationToken);
        if (mediaError is { } error)
            return error;

        var now = dateTimeProvider.UtcNow;

        var resolvedTags = await ResolveTagsAsync(request.Tags, userId, now, cancellationToken);
        if (resolvedTags.IsError)
            return resolvedTags.Errors;

        var tags = resolvedTags.Value;
        var postResult = Post.Create(
            userId, request.Content, MapSensitivity(request.Sensitivity), now, mediaIds,
            tags.Select(tag => tag.Id).ToList());
        if (postResult.IsError)
            return postResult.Errors;

        // The tag entities are tracked by the same scoped context, so AddAsync's SaveChanges
        // persists the post together with the usage-count bumps (profile-tags precedent).
        foreach (var tag in tags)
            tag.RecordPostAdded(now);

        var post = postResult.Value;
        await postRepository.AddAsync(post, cancellationToken);

        var mediaByPost = await mediaResponseFactory.ForPostsAsync([post], cancellationToken);
        return PostResponse.Create(
            post, author, isLikedByCurrentUser: false, mediaByPost.GetValueOrDefault(post.Id, []),
            tags.Select(PostTagResponse.Create).ToList());
    }

    // Same resolution flow as UpdateProfileTags (normalise → resolve aliases → create missing →
    // attachable check); duplicated for now since the profile flow is interleaved with tier
    // handling — extract a shared resolver when a third caller appears.
    private async Task<ErrorOr<List<Tag>>> ResolveTagsAsync(
        IReadOnlyList<string> tagNames,
        Ulid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var tags = new List<Tag>(tagNames.Count);
        var seenSlugs = new HashSet<string>();
        var seenTagIds = new HashSet<Ulid>();
        List<Tag> newTags = [];

        foreach (var name in tagNames)
        {
            var normalized = normalizer.Normalize(name);
            if (normalized.IsError)
                return normalized.Errors;

            if (!seenSlugs.Add(normalized.Value.Slug))
                return TagErrors.Duplicate;

            var tag = await tagRepository.GetBySlugResolvingAliasAsync(normalized.Value.Slug, cancellationToken);
            if (tag is null)
            {
                tag = Tag.Create(normalized.Value.DisplayName, normalized.Value.Slug, userId, now);
                newTags.Add(tag);
            }

            if (!tag.IsAttachable)
                return TagErrors.Banned;

            // Distinct slugs can still resolve to the same tag through aliases.
            if (!seenTagIds.Add(tag.Id))
                return TagErrors.Duplicate;

            tags.Add(tag);
        }

        if (newTags.Count == 0)
            return tags;

        await tagRepository.AddRangeAsync(newTags, cancellationToken);
        await tagRepository.UpdateAsync(cancellationToken);

        return tags;
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

        // Union across posts AND messages, so the same upload cannot be attached twice by
        // going out through a DM first.
        var attached = await mediaRepository.GetAttachedIdsAsync(mediaIds, cancellationToken);
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
