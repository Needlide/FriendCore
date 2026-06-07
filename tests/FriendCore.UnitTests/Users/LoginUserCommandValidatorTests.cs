using FriendCore.Application.Common.Validation;
using FriendCore.Application.Users.Commands.LoginUser;

namespace FriendCore.UnitTests.Users;

public class LoginUserCommandValidatorTests
{
    private readonly LoginUserCommandValidator _validator = new();

    [Fact]
    public void Validate_Passes_ForValidCredentials()
    {
        var result = _validator.Validate(new LoginUserCommand("alice@example.com", "password"));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_Fails_WhenEmailEmpty(string email)
    {
        var result = _validator.Validate(new LoginUserCommand(email, "password"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == "User.Email.Required");
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing-domain@")]
    public void Validate_Fails_WhenEmailMalformed(string email)
    {
        var result = _validator.Validate(new LoginUserCommand(email, "password"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == "User.Email.InvalidFormat");
    }

    [Fact]
    public void Validate_Fails_WhenPasswordEmpty()
    {
        var result = _validator.Validate(new LoginUserCommand("alice@example.com", ""));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == "User.Password.Required");
    }

    [Fact]
    public void Validate_Fails_WhenPasswordTooLong()
    {
        var password = new string('a', PasswordValidationRules.MaxLength + 1);

        var result = _validator.Validate(new LoginUserCommand("alice@example.com", password));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == PasswordValidationRules.TooLongCode);
    }
}
