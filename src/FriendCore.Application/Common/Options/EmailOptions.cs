namespace FriendCore.Application.Common.Options;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string FromAddress { get; init; } = "noreply@friendcore.local";

    public string FromName { get; init; } = "FriendCore";

    public string? ResendApiKey { get; init; }

    /// <summary>
    ///     Base URL for verification links (typically the web app), e.g. https://app.friendcore.com.
    ///     The handler appends /verify-email?token=...
    /// </summary>
    public string VerificationBaseUrl { get; init; } = "http://localhost:3000";

    public int VerificationTokenExpirationHours { get; init; } = 24;

    /// <summary>
    ///     Base URL for password-reset links. The handler appends /reset-password?token=...
    /// </summary>
    public string ResetBaseUrl { get; init; } = "http://localhost:3000";

    public int ResetTokenExpirationHours { get; init; } = 2;
}