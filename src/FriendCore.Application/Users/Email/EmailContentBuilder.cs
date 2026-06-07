using System.Net;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Options;

namespace FriendCore.Application.Users.Email;

internal static class EmailContentBuilder
{
    public static EmailMessage Welcome(string to, string displayName, EmailOptions options)
    {
        return new EmailMessage(
            to,
            "Welcome to FriendCore",
            $"""
             <p>Hi {Encode(displayName)},</p>
             <p>Thanks for joining FriendCore. We are glad you are here.</p>
             <p>Please verify your email address so you can post and use all features.</p>
             <p>— The FriendCore team</p>
             """,
            $"Hi {displayName},\n\nThanks for joining FriendCore. Please verify your email to unlock posting.\n\n— The FriendCore team");
    }

    public static EmailMessage Verification(
        string to,
        string displayName,
        string verificationUrl,
        EmailOptions options)
    {
        return new EmailMessage(
            to,
            "Verify your FriendCore email",
            $"""
             <p>Hi {Encode(displayName)},</p>
             <p>Click the link below to verify your email address. This link expires in {options.VerificationTokenExpirationHours} hours.</p>
             <p><a href="{Encode(verificationUrl)}">Verify email</a></p>
             <p>If you did not create an account, you can ignore this email.</p>
             """,
            $"Hi {displayName},\n\nVerify your email: {verificationUrl}\n\nThis link expires in {options.VerificationTokenExpirationHours} hours.");
    }

    public static string BuildVerificationUrl(EmailOptions options, string rawToken)
    {
        var baseUrl = options.VerificationBaseUrl.TrimEnd('/');
        return $"{baseUrl}/verify-email?token={Uri.EscapeDataString(rawToken)}";
    }

    private static string Encode(string value)
    {
        return WebUtility.HtmlEncode(value);
    }
}