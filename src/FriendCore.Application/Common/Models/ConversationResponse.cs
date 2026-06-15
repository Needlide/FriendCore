namespace FriendCore.Application.Common.Models;

public sealed record ConversationResponse(
    string Id,
    UserSummaryResponse OtherParticipant,
    DateTimeOffset LastMessageAt,
    int UnreadCount);

public sealed record ConversationListResponse(IReadOnlyList<ConversationResponse> Conversations);
