using FriendCore.Application.Common.Models;
using FriendCore.Domain.Posts;
using FriendCore.Domain.Tags;
using FriendCore.Domain.Users;

using FriendCore.Application.Common;

namespace FriendCore.Application.Posts.Common;

/// <summary>
///     Builds full <see cref="PostResponse" /> pages: authors, viewer like-flags, media
///     variants, and tag names — one batched lookup each, input order preserved. Posts whose
///     author cannot be resolved (hidden by the block filter, deleted) are skipped. Shared by
///     the home feed, user posts, discovery posts, and single-post queries.
/// </summary>
internal sealed class PostResponseAssembler(
    IUserRepository userRepository,
    IPostRepository postRepository,
    ITagRepository tagRepository,
    MediaResponseFactory mediaResponseFactory)
{
    public async Task<List<PostResponse>> AssembleAsync(
        IReadOnlyList<Post> posts,
        Ulid? viewerId,
        CancellationToken cancellationToken)
    {
        if (posts.Count == 0)
            return [];

        var authors = (await userRepository.GetByIdsAsync(
                posts.Select(post => post.AuthorId).Distinct().ToList(), cancellationToken))
            .ToDictionary(user => user.Id);

        var likedPostIds = viewerId is { } viewer
            ? (await postRepository.GetLikedPostIdsAsync(
                viewer, posts.Select(post => post.Id).ToList(), cancellationToken)).ToHashSet()
            : [];

        var mediaByPost = await mediaResponseFactory.ForPostsAsync(posts, cancellationToken);
        var tagsById = await LoadTagsAsync(posts, cancellationToken);

        return posts
            .Where(post => authors.ContainsKey(post.AuthorId))
            .Select(post => PostResponse.Create(
                post,
                authors[post.AuthorId],
                likedPostIds.Contains(post.Id),
                mediaByPost.GetValueOrDefault(post.Id, []),
                post.Tags
                    .Where(tag => tagsById.ContainsKey(tag.TagId))
                    .Select(tag => PostTagResponse.Create(tagsById[tag.TagId]))
                    .ToList()))
            .ToList();
    }

    private async Task<Dictionary<Ulid, Tag>> LoadTagsAsync(
        IReadOnlyList<Post> posts,
        CancellationToken cancellationToken)
    {
        var tagIds = posts.SelectMany(post => post.Tags.Select(tag => tag.TagId)).Distinct().ToList();
        if (tagIds.Count == 0)
            return [];

        return (await tagRepository.GetByIdsAsync(tagIds, cancellationToken)).ToDictionary(tag => tag.Id);
    }
}
