using FriendCore.Application.Common.Models;
using FriendCore.Application.Common.Options;
using FriendCore.Domain.Media;
using FriendCore.Domain.Messaging;
using FriendCore.Domain.Posts;
using Microsoft.Extensions.Options;

namespace FriendCore.Application.Common;

/// <summary>
///     Builds the media payload for post and message responses: one media lookup for a whole
///     page, public URLs composed from the stored keys + StorageOptions.PublicBaseUrl
///     (keys-not-URLs: a CDN/bucket move is config-only).
///
///     Lives in Common, not Posts, now that messaging attaches media too — a chat bubble
///     embeds the same variants a feed card does, and duplicating the URL-joining here
///     would fork the one piece of this logic that silently breaks images when wrong.
/// </summary>
internal sealed class MediaResponseFactory(
    IMediaRepository mediaRepository,
    IOptions<StorageOptions> storageOptions)
{
    private readonly string? _publicBaseUrl = storageOptions.Value.PublicBaseUrl?.TrimEnd('/');

    public async Task<IReadOnlyDictionary<Ulid, IReadOnlyList<MediaResponse>>> ForPostsAsync(
        IReadOnlyCollection<Post> posts,
        CancellationToken cancellationToken)
    {
        var mediaIds = posts
            .SelectMany(post => post.Media)
            .Select(postMedia => postMedia.MediaItemId)
            .Distinct()
            .ToList();

        if (mediaIds.Count == 0)
            return new Dictionary<Ulid, IReadOnlyList<MediaResponse>>();

        var mediaById = await LoadAsync(mediaIds, cancellationToken);

        return posts.ToDictionary(
            post => post.Id,
            post => (IReadOnlyList<MediaResponse>)post.Media
                .OrderBy(postMedia => postMedia.DisplayOrder)
                .Select(postMedia => mediaById.GetValueOrDefault(postMedia.MediaItemId))
                // Soft-deleted media items fall out of the lookup; the post simply omits them.
                .Where(media => media is not null)
                .Select(media => MediaResponse.Create(media!, _publicBaseUrl))
                .ToList());
    }

    /// <summary>Per-message media for a page of the thread. One lookup for the whole page.</summary>
    public async Task<IReadOnlyDictionary<Ulid, IReadOnlyList<MediaResponse>>> ForMessagesAsync(
        IReadOnlyCollection<Message> messages,
        CancellationToken cancellationToken)
    {
        var mediaIds = messages
            .SelectMany(message => message.Media)
            .Select(messageMedia => messageMedia.MediaItemId)
            .Distinct()
            .ToList();

        if (mediaIds.Count == 0)
            return new Dictionary<Ulid, IReadOnlyList<MediaResponse>>();

        var mediaById = await LoadAsync(mediaIds, cancellationToken);

        return messages.ToDictionary(
            message => message.Id,
            message => (IReadOnlyList<MediaResponse>)message.Media
                .OrderBy(messageMedia => messageMedia.DisplayOrder)
                .Select(messageMedia => mediaById.GetValueOrDefault(messageMedia.MediaItemId))
                // A soft-deleted media item leaves the bubble without that image rather than
                // failing the whole page. With several images the rest still render.
                .Where(media => media is not null)
                .Select(media => MediaResponse.Create(media!, _publicBaseUrl))
                .ToList());
    }

    private async Task<Dictionary<Ulid, MediaItem>> LoadAsync(
        IReadOnlyCollection<Ulid> ids,
        CancellationToken cancellationToken) =>
        (await mediaRepository.GetByIdsAsync(ids, cancellationToken)).ToDictionary(media => media.Id);
}
