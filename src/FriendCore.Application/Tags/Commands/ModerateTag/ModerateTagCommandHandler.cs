using ErrorOr;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Tags;
using MediatR;

namespace FriendCore.Application.Tags.Commands.ModerateTag;

internal sealed class ModerateTagCommandHandler(ITagRepository tagRepository)
    : IRequestHandler<ModerateTagCommand, ErrorOr<TagResponse>>
{
    public async Task<ErrorOr<TagResponse>> Handle(ModerateTagCommand request, CancellationToken cancellationToken)
    {
        var tag = await tagRepository.GetByIdAsync(request.TagId, cancellationToken);
        if (tag is null)
            return TagErrors.NotFound;

        var outcome = Apply(tag, request.Action);
        if (outcome.IsError)
            return outcome.Errors;

        await tagRepository.UpdateAsync(cancellationToken);
        return TagResponse.FromTag(tag);
    }

    private static ErrorOr<Success> Apply(Tag tag, TagModerationAction action)
    {
        switch (action)
        {
            case TagModerationAction.Approve:
                return tag.Approve();
            case TagModerationAction.Archive:
                return tag.Archive();
            case TagModerationAction.Ban:
                tag.Ban();
                return Result.Success;
            default:
                return Error.Validation("Tag.Moderation.InvalidAction", "Moderation action is invalid.");
        }
    }
}
