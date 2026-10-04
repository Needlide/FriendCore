namespace FriendCore.Application.Common.Models;

/// <summary>
/// The variant URLs for one media item, handed out only after an authorized consent step
/// (see <c>RevealMediaCommand</c>).
/// </summary>
/// <param name="GateReason">
/// Repeated from <see cref="MediaResponse.GateReason" /> so the client does not have to correlate
/// this response with the list response it came from — the same reasoning as sending the reason in
/// the first place: the server decides, the client obeys.
/// </param>
public sealed record MediaVariantsResponse(
    string MediaId,
    IReadOnlyList<MediaVariantResponse> Variants,
    string? GateReason = null);
