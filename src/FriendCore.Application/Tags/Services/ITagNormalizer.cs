using ErrorOr;

namespace FriendCore.Application.Tags.Services;

public interface ITagNormalizer
{
    // Write-time normalization with validation. DisplayName preserves the user's original
    // casing (trimmed); Slug is the normalized, uniqueness-bearing form.
    ErrorOr<NormalizedTag> Normalize(string input);

    // Raw slug transform without validation, for query prefixes (e.g. autocomplete).
    string ToSlug(string input);
}

public sealed record NormalizedTag(string DisplayName, string Slug);
