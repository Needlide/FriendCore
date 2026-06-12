namespace FriendCore.Application.Common.Models;

public sealed record DiscoveryPersonResponse(
    string Id,
    string Username,
    string DisplayName,
    string? Bio,
    int MatchScore);

public sealed record DiscoveryPeopleResponse(IReadOnlyList<DiscoveryPersonResponse> People);
