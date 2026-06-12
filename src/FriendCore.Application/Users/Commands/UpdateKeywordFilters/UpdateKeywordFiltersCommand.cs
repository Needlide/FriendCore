using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Users.Commands.UpdateKeywordFilters;

/// <summary>Whole-set replace; returns the normalised list actually stored.</summary>
public sealed record UpdateKeywordFiltersCommand(IReadOnlyList<string> Keywords)
    : IRequest<ErrorOr<KeywordFiltersResponse>>;
