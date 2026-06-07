namespace FriendCore.Application.Common.Models;

public sealed record TagAutocompleteResponse(
    string Query,
    IReadOnlyList<TagSuggestion> Suggestions,
    bool HasExactMatch);

public sealed record TagSuggestion(
    string Id,
    string Name,
    string Slug,
    int TotalCount);
