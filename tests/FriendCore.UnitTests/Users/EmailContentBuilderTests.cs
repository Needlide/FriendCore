using FriendCore.Application.Common.Options;
using FriendCore.Application.Users.Email;

namespace FriendCore.UnitTests.Users;

public class EmailContentBuilderTests
{
    private static EmailOptions Options(string baseUrl = "https://app.friendcore.com", int hours = 24)
    {
        return new EmailOptions { VerificationBaseUrl = baseUrl, VerificationTokenExpirationHours = hours };
    }

    [Fact]
    public void BuildVerificationUrl_TrimsTrailingSlashAndAppendsPath()
    {
        var url = EmailContentBuilder.BuildVerificationUrl(Options("https://app.friendcore.com/"), "abc123");

        Assert.Equal("https://app.friendcore.com/verify-email?token=abc123", url);
    }

    [Fact]
    public void BuildVerificationUrl_EscapesToken()
    {
        var url = EmailContentBuilder.BuildVerificationUrl(Options(), "a+b/c=d");

        Assert.Contains("token=a%2Bb%2Fc%3Dd", url);
    }

    [Fact]
    public void Verification_IncludesUrlAndExpirationHours()
    {
        var url = "https://app.friendcore.com/verify-email?token=abc";

        var message = EmailContentBuilder.Verification("alice@example.com", "Alice", url, Options(hours: 12));

        Assert.Equal("alice@example.com", message.To);
        Assert.Equal("Verify your FriendCore email", message.Subject);
        Assert.Contains(url, message.HtmlBody);
        Assert.Contains("12 hours", message.HtmlBody);
        Assert.NotNull(message.PlainTextBody);
        Assert.Contains(url, message.PlainTextBody!);
    }

    [Fact]
    public void Verification_HtmlEncodesDisplayName()
    {
        var message = EmailContentBuilder.Verification(
            "alice@example.com",
            "<script>alert(1)</script>",
            "https://app.friendcore.com/verify-email?token=abc",
            Options());

        Assert.DoesNotContain("<script>alert(1)</script>", message.HtmlBody);
        Assert.Contains("&lt;script&gt;", message.HtmlBody);
    }

    [Fact]
    public void Welcome_SetsRecipientAndSubject()
    {
        var message = EmailContentBuilder.Welcome("alice@example.com", "Alice", Options());

        Assert.Equal("alice@example.com", message.To);
        Assert.Equal("Welcome to FriendCore", message.Subject);
    }
}