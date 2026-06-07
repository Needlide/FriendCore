using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Tags;
using MediatR;

namespace FriendCore.Application.Tags.Commands.ArchiveStaleTags;

internal sealed class ArchiveStaleTagsCommandHandler(
    ITagRepository tagRepository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<ArchiveStaleTagsCommand, ErrorOr<StaleTagsArchivedResponse>>
{
    // §6.8: tags below 3 users AND idle for 180 days are auto-archived.
    private const int MinProfileCount = 3;
    private static readonly TimeSpan IdlePeriod = TimeSpan.FromDays(180);

    public async Task<ErrorOr<StaleTagsArchivedResponse>> Handle(
        ArchiveStaleTagsCommand request,
        CancellationToken cancellationToken)
    {
        var cutoff = dateTimeProvider.UtcNow - IdlePeriod;
        var stale = await tagRepository.GetArchivableAsync(MinProfileCount, cutoff, cancellationToken);

        if (stale.Count == 0)
            return new StaleTagsArchivedResponse(0);

        foreach (var tag in stale)
            tag.Archive();

        await tagRepository.UpdateAsync(cancellationToken);
        return new StaleTagsArchivedResponse(stale.Count);
    }
}
