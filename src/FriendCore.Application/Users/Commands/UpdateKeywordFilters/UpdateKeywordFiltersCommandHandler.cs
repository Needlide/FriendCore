using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Common;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Users.Commands.UpdateKeywordFilters;

internal sealed class UpdateKeywordFiltersCommandHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<UpdateKeywordFiltersCommand, ErrorOr<KeywordFiltersResponse>>
{
    public async Task<ErrorOr<KeywordFiltersResponse>> Handle(
        UpdateKeywordFiltersCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return CommonErrors.Unauthorized;

        var preferences = await userRepository.GetPreferencesAsync(userId, cancellationToken);
        if (preferences is null)
            return UserErrors.NotFound;

        var result = preferences.SetKeywordFilters(request.Keywords, dateTimeProvider.UtcNow);
        if (result.IsError)
            return result.Errors;

        await userRepository.UpdatePreferencesAsync(cancellationToken);
        return new KeywordFiltersResponse(preferences.KeywordFilters);
    }
}
