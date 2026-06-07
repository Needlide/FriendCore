using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Tags.Commands.ArchiveStaleTags;

public sealed record ArchiveStaleTagsCommand : IRequest<ErrorOr<StaleTagsArchivedResponse>>;
