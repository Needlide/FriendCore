namespace FriendCore.Application.Common.Models;

// Authoritative like state returned by the like/unlike endpoints so the client can reconcile its
// optimistic update from the mutation's own response, with no follow-up read.
public sealed record PostLikeResponse(bool Liked, int LikeCount);
