using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Application.Tags.Services;
using FriendCore.Domain.Common;
using FriendCore.Domain.Tags;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Tags.Commands.UpdateProfileTags;

internal sealed class UpdateProfileTagsCommandHandler(
    IUserRepository userRepository,
    ITagRepository tagRepository,
    ITagNormalizer normalizer,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<UpdateProfileTagsCommand, ErrorOr<ProfileTagsResponse>>
{
    public async Task<ErrorOr<ProfileTagsResponse>> Handle(
        UpdateProfileTagsCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return CommonErrors.Unauthorized;

        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
            return UserErrors.NotFound;

        var now = dateTimeProvider.UtcNow;

        var resolved = await ResolveTagsAsync(request.Tags, userId, now, cancellationToken);
        if (resolved.IsError)
            return resolved.Errors;

        var assignments = resolved.Value;

        if (assignments.Count(a => a.Tier == TagTier.Primary) > TagLimits.MaxPrimaryProfileTags)
            return TagErrors.TooManyPrimaryTags;
        if (assignments.Count(a => a.Tier == TagTier.Secondary) > TagLimits.MaxSecondaryProfileTags)
            return TagErrors.TooManySecondaryTags;

        await ReconcileAsync(user, assignments, now, cancellationToken);

        // Single SaveChanges over the shared DbContext persists the user's profile-tag
        // changes together with the tag usage-count mutations made in ReconcileAsync.
        await userRepository.UpdateAsync(user, cancellationToken);

        var items = assignments
            .Select(a => new ProfileTagItem(a.Tag.Id.ToString(), a.Tag.Name, a.Tag.Slug, a.Tier.ToString(), a.IsPublic))
            .ToList();

        return new ProfileTagsResponse(items);
    }

    private async Task<ErrorOr<List<TagAssignment>>> ResolveTagsAsync(
        IReadOnlyList<ProfileTagInput> inputs,
        Ulid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var assignments = new List<TagAssignment>(inputs.Count);
        var seenSlugs = new HashSet<string>();
        var seenTagIds = new HashSet<Ulid>();

        foreach (var input in inputs)
        {
            var normalized = normalizer.Normalize(input.Name);
            if (normalized.IsError)
                return normalized.Errors;

            if (!seenSlugs.Add(normalized.Value.Slug))
                return TagErrors.Duplicate;

            // Follows aliases so a merged tag's old name resolves to its target.
            var tag = await tagRepository.GetBySlugResolvingAliasAsync(normalized.Value.Slug, cancellationToken);
            if (tag is null)
            {
                tag = Tag.Create(normalized.Value.DisplayName, normalized.Value.Slug, userId, now);
                await tagRepository.AddAsync(tag, cancellationToken);
            }

            if (!tag.IsAttachable)
                return TagErrors.Banned;

            // Distinct slugs can still resolve to the same tag through aliases.
            if (!seenTagIds.Add(tag.Id))
                return TagErrors.Duplicate;

            assignments.Add(new TagAssignment(tag, input.Tier, input.IsPublic));
        }

        return assignments;
    }

    private async Task ReconcileAsync(
        User user,
        List<TagAssignment> assignments,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var existingTagIds = user.ProfileTags.Select(tag => tag.TagId).ToHashSet();
        var desiredTagIds = assignments.Select(a => a.Tag.Id).ToHashSet();

        var removedTagIds = existingTagIds.Where(id => !desiredTagIds.Contains(id)).ToList();
        if (removedTagIds.Count > 0)
        {
            var removedTags = await tagRepository.GetByIdsAsync(removedTagIds, cancellationToken);
            foreach (var tag in removedTags)
                tag.RecordProfileRemoved();

            foreach (var tagId in removedTagIds)
                user.RemoveProfileTag(tagId);
        }

        // Retire existing assignments first so freed tier slots are available before adds.
        foreach (var assignment in assignments.Where(a => existingTagIds.Contains(a.Tag.Id)))
            user.UpdateProfileTag(assignment.Tag.Id, assignment.Tier, assignment.IsPublic, now);

        foreach (var assignment in assignments.Where(a => !existingTagIds.Contains(a.Tag.Id)))
        {
            user.AddProfileTag(assignment.Tag.Id, assignment.Tier, assignment.IsPublic, now);
            assignment.Tag.RecordProfileAdded(now);
        }
    }

    private sealed record TagAssignment(Tag Tag, TagTier Tier, bool IsPublic);
}