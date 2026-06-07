using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Application.Tags.Services;
using FriendCore.Domain.Common;
using FriendCore.Domain.Tags;
using MediatR;

namespace FriendCore.Application.Tags.Commands.CreateOrGetTag;

internal sealed class CreateOrGetTagCommandHandler(
    ITagRepository tagRepository,
    ITagNormalizer normalizer,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<CreateOrGetTagCommand, ErrorOr<TagResponse>>
{
    public async Task<ErrorOr<TagResponse>> Handle(CreateOrGetTagCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return CommonErrors.Unauthorized;

        var normalized = normalizer.Normalize(request.Name);
        if (normalized.IsError)
            return normalized.Errors;

        var existing = await tagRepository.GetBySlugAsync(normalized.Value.Slug, cancellationToken);
        if (existing is not null)
            return TagResponse.FromTag(existing);

        var tag = Tag.Create(normalized.Value.DisplayName, normalized.Value.Slug, userId, dateTimeProvider.UtcNow);
        await tagRepository.AddAsync(tag, cancellationToken);

        return TagResponse.FromTag(tag);
    }
}
