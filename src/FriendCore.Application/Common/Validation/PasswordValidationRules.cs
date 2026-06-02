using FluentValidation;

namespace FriendCore.Application.Common.Validation;

/// <summary>
///     NIST SP 800-63B–aligned password rules for registration (MVP).
///     Length over composition; no mandatory symbols. Common-password / breach checks are post-MVP.
/// </summary>
public static class PasswordValidationRules
{
    public const int MinLength = 8;
    public const int MaxLength = 128;

    public const string RequiredCode = "User.Password.Required";
    public const string TooShortCode = "User.Password.TooShort";
    public const string TooLongCode = "User.Password.TooLong";
    public const string WhitespaceOnlyCode = "User.Password.WhitespaceOnly";

    public static IRuleBuilderOptions<T, string> ApplyRegistrationPasswordRules<T>(
        this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder
            .NotEmpty()
            .WithErrorCode(RequiredCode)
            .WithMessage("Password is required.")
            .MinimumLength(MinLength)
            .WithErrorCode(TooShortCode)
            .WithMessage($"Password must be at least {MinLength} characters.")
            .MaximumLength(MaxLength)
            .WithErrorCode(TooLongCode)
            .WithMessage($"Password must not exceed {MaxLength} characters.")
            .Must(ContainsNonWhitespaceCharacter)
            .WithErrorCode(WhitespaceOnlyCode)
            .WithMessage("Password cannot be blank or whitespace only.");
    }

    private static bool ContainsNonWhitespaceCharacter(string password)
    {
        return password.Any(c => !char.IsWhiteSpace(c));
    }
}