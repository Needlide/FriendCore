using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Tags.Commands.MergeTags;

public sealed record MergeTagsCommand(Ulid SourceTagId, Ulid TargetTagId) : IRequest<ErrorOr<TagResponse>>;
