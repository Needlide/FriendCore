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

    /// <summary>
    ///     When true, only verified users may create posts. Set to false in development
    ///     where no email service is configured; set to true when Resend is configured
    ///     and you want the verification gate enforced.
    /// </summary>
    public bool RequireVerificationForPosting { get; init; } = true;

    /// <summary>
    ///     When true, a new registration is marked verified immediately and no verification
    ///     email is sent. Intended for preview/demo deployments where no mail provider is
    ///     configured, so there is no way for a new user to complete verification. Off by
    ///     default; production must leave this false and configure Resend instead.
    /// </summary>
    public bool AutoVerifyNewUsers { get; init; }
}