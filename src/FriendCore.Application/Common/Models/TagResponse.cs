using FriendCore.Domain.Tags;

namespace FriendCore.Application.Common.Models;

public sealed record TagResponse(
    string Id,
    string Name,
    string Slug,
    string Status,
    int TotalCount)
{
    public static TagResponse FromTag(Tag tag) =>
        new(tag.Id.ToString(), tag.Name, tag.Slug, tag.Status.ToString(), tag.TotalCount);
}
