namespace FriendCore.API.Contracts.Media;

/// <summary>
///     Multipart form upload. Safety flags arrive as ONE JSON object
///     (<c>{ "flashing": true, "arachnophobia": false }</c>) rather than one bool per hazard, so
///     adding a new flag never changes this contract or the client form.
/// </summary>
/// <remarks>
///     Kept as a string here on purpose: model binding a dictionary straight from multipart would
///     look for <c>SafetyFlags[key]</c> fields, not a JSON value. <c>MediaController</c> parses it.
/// </remarks>
public sealed record UploadMediaRequest(IFormFile? File, string? SafetyFlags);
