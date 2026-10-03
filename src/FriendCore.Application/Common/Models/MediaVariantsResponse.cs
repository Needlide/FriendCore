namespace FriendCore.Application.Common.Models;

/// <summary>
/// The variant URLs for one media item, handed out only after an authorized consent step
/// (see <c>RevealMediaCommand</c>).
/// </summary>
public sealed record MediaVariantsResponse(
    string MediaId,
    IReadOnlyList<MediaVariantResponse> Variants);
