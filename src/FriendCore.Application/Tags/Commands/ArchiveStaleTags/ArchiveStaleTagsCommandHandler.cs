using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Application.Common.Options;
using FriendCore.Domain.Tags;
using MediatR;
using Microsoft.Extensions.Options;

namespace FriendCore.Application.Tags.Commands.ArchiveStaleTags;

internal sealed class ArchiveStaleTagsCommandHandler(
    ITagRepository tagRepository,
    IDateTimeProvider dateTimeProvider,
    IOptions<TagMaintenanceOptions> options)
    : IRequestHandler<ArchiveStaleTagsCommand, ErrorOr<StaleTagsArchivedResponse>>
{
    // §6.8: tags below 3 users AND idle for 180 days are auto-archived.
    private const int MinProfileCount = 3;
    private static readonly TimeSpan IdlePeriod = TimeSpan.FromDays(180);

    private readonly int _batchSize = options.Value.ArchiveBatchSize;

    public async Task<ErrorOr<StaleTagsArchivedResponse>> Handle(
        ArchiveStaleTagsCommand request,
        CancellationToken cancellationToken)
    {
        var cutoff = dateTimeProvider.UtcNow - IdlePeriod;
        var totalArchived = 0;

        // Drain in batches: each archived batch leaves the filter, so the next read returns
        // the following stale tags. No offset to track; loop until a short batch signals the end.
        while (true)
        {
            var batch = await tagRepository.GetArchivableAsync(MinProfileCount, cutoff, _batchSize, cancellationToken);
            if (batch.Count == 0)
                break;

            foreach (var tag in batch)
                tag.Archive();

            await tagRepository.UpdateAsync(cancellationToken);
            totalArchived += batch.Count;

            if (batch.Count < _batchSize)
                break;
        }

        return new StaleTagsArchivedResponse(totalArchived);
    }
}