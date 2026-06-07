using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Tags;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Tags.Commands.MergeTags;

internal sealed class MergeTagsCommandHandler(
    ITagRepository tagRepository,
    IUserRepository userRepository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<MergeTagsCommand, ErrorOr<TagResponse>>
{
    public async Task<ErrorOr<TagResponse>> Handle(MergeTagsCommand request, CancellationToken cancellationToken)
    {
        if (request.SourceTagId == request.TargetTagId)
            return TagErrors.CannotMergeIntoSelf;

        var source = await tagRepository.GetByIdAsync(request.SourceTagId, cancellationToken);
        if (source is null)
            return TagErrors.NotFound;

        var target = await tagRepository.GetByIdAsync(request.TargetTagId, cancellationToken);
        if (target is null)
            return TagErrors.NotFound;

        if (source.Status == TagStatus.Merged)
            return TagErrors.AlreadyMerged;
        if (source.Status == TagStatus.Banned)
            return TagErrors.Banned;
        if (target.Status != TagStatus.Active)
            return TagErrors.InvalidMergeTarget;

        var now = dateTimeProvider.UtcNow;

        await RepointProfileUsagesAsync(source, target, now, cancellationToken);

        var mergeResult = source.MergeInto(target.Id);
        if (mergeResult.IsError)
            return mergeResult.Errors;

        // Single SaveChanges over the shared DbContext persists the re-pointed user profiles
        // together with the source/target tag changes.
        await tagRepository.UpdateAsync(cancellationToken);
        return TagResponse.FromTag(target);
    }

    private async Task RepointProfileUsagesAsync(Tag source, Tag target, DateTimeOffset now, CancellationToken ct)
    {
        var users = await userRepository.GetUsersWithProfileTagAsync(source.Id, ct);

        foreach (var user in users)
        {
            var existing = user.ProfileTags.FirstOrDefault(tag => tag.TagId == source.Id);
            if (existing is null)
                continue;

            var alreadyHasTarget = user.ProfileTags.Any(tag => tag.TagId == target.Id);
            var tier = existing.Tier;
            var isPublic = existing.IsPublic;

            user.RemoveProfileTag(source.Id);

            // Keep the target's existing assignment when the user already has it; otherwise
            // re-point the source assignment to the target (preserving tier and visibility).
            if (!alreadyHasTarget)
            {
                user.AddProfileTag(target.Id, tier, isPublic, now);
                target.RecordProfileAdded(now);
            }
        }
    }
}
