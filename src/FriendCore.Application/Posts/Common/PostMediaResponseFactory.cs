using FriendCore.Application.Common.Models;
using FriendCore.Application.Common.Options;
using FriendCore.Domain.Media;
using FriendCore.Domain.Posts;
using Microsoft.Extensions.Options;

namespace FriendCore.Application.Posts.Common;

/// <summary>
///     Builds the media payload for post responses: one media lookup for a whole page of posts,
///     public URLs composed from the stored keys + StorageOptions.PublicBaseUrl (keys-not-URLs:
///     a CDN/bucket move is config-only).
/// </summary>
internal sealed class PostMediaResponseFactory(
    IMediaRepository mediaRepository,
    IOptions<StorageOptions> storageOptions)
{
    private readonly string? _publicBaseUrl = storageOptions.Value.PublicBaseUrl?.TrimEnd('/');

    public async Task<IReadOnlyDictionary<Ulid, IReadOnlyList<PostMediaResponse>>> ForPostsAsync(
        IReadOnlyCollection<Post> posts,
        CancellationToken cancellationToken)
    {
        var mediaIds = posts
            .SelectMany(post => post.Media)
            .Select(postMedia => postMedia.MediaItemId)
            .Distinct()
            .ToList();

        if (mediaIds.Count == 0)
            return new Dictionary<Ulid, IReadOnlyList<PostMediaResponse>>();

        var mediaById = (await mediaRepository.GetByIdsAsync(mediaIds, cancellationToken))
            .ToDictionary(media => media.Id);

        return posts.ToDictionary(
            post => post.Id,
            post => (IReadOnlyList<PostMediaResponse>)post.Media
                .OrderBy(postMedia => postMedia.DisplayOrder)
                .Select(postMedia => mediaById.GetValueOrDefault(postMedia.MediaItemId))
                // Soft-deleted media items fall out of the lookup; the post simply omits them.
                .Where(media => media is not null)
                .Select(media => PostMediaResponse.Create(media!, _publicBaseUrl))
                .ToList());
    }
}
