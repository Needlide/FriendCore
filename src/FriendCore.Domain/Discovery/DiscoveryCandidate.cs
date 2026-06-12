using FriendCore.Domain.Users;

namespace FriendCore.Domain.Discovery;

/// <summary>A candidate profile with its §6.6 tag-overlap score for a specific viewer.</summary>
public sealed record DiscoveryCandidate(User User, int Score);
