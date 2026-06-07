using ErrorOr;

namespace FriendCore.Domain.Tags;

public static class TagErrors
{
    public static Error NotFound =>
        Error.NotFound("Tag.NotFound", "Tag was not found.");

    public static Error Banned =>
        Error.Validation("Tag.Banned", "This tag is not available for use.");

    public static Error TooShort =>
        Error.Validation("Tag.TooShort", $"Tag must be at least {TagLimits.MinSlugLength} characters.");

    public static Error TooLong =>
        Error.Validation("Tag.TooLong", $"Tag must not exceed {TagLimits.MaxSlugLength} characters.");

    public static Error Empty =>
        Error.Validation("Tag.Empty", "Tag contains no usable characters.");

    public static Error BannedWord =>
        Error.Validation("Tag.BannedWord", "This tag is not allowed.");

    public static Error Duplicate =>
        Error.Validation("Tag.Duplicate", "The same tag cannot be added to a profile twice.");

    public static Error TooManyPrimaryTags =>
        Error.Validation("Tag.TooManyPrimary",
            $"A profile can have at most {TagLimits.MaxPrimaryProfileTags} primary tags.");

    public static Error TooManySecondaryTags =>
        Error.Validation("Tag.TooManySecondary",
            $"A profile can have at most {TagLimits.MaxSecondaryProfileTags} secondary tags.");
}