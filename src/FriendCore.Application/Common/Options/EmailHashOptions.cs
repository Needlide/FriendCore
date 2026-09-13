namespace FriendCore.Application.Common.Options;

/// <summary>
///     Pepper for the deterministic email hash (HMAC-SHA256). The pepper lives in Secrets
///     Manager / user-secrets — never in the database, never in code. Rotation invalidates
///     every stored hash with no migration path, so rotate only on suspected compromise.
/// </summary>
public sealed class EmailHashOptions
{
    public const string SectionName = "EmailHashing";

    public string Pepper { get; set; } = string.Empty;
}
