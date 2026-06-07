using FriendCore.Application.Users.Commands.VerifyEmail;

namespace FriendCore.UnitTests.Users;

public class VerifyEmailCommandValidatorTests
{
    private readonly VerifyEmailCommandValidator _validator = new();

    [Fact]
    public void Validate_Passes_ForValidToken()
    {
        var result = _validator.Validate(new VerifyEmailCommand("a-valid-token"));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_Fails_WhenTokenEmpty(string token)
    {
        var result = _validator.Validate(new VerifyEmailCommand(token));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == "User.VerificationToken.Required");
    }

    [Fact]
    public void Validate_Fails_WhenTokenTooLong()
    {
        var result = _validator.Validate(new VerifyEmailCommand(new string('a', 257)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == "User.VerificationToken.TooLong");
    }
}