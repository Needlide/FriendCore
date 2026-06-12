namespace FriendCore.API.Contracts.Users;

/// <summary>Whole-set replace: the submitted list becomes the user's complete filter set.</summary>
public sealed record UpdateKeywordFiltersRequest(IReadOnlyList<string> Keywords);
