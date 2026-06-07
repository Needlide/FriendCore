using System.Globalization;
using System.Text;
using ErrorOr;
using FriendCore.Domain.Tags;

namespace FriendCore.Application.Tags.Services;

public sealed class TagNormalizationService : ITagNormalizer
{
    // Stub for §6.2 step 6. Fill this set later to enforce a banned-word list without
    // changing the interface. Slugs are matched in normalized (lowercase, hyphenated) form.
    private static readonly HashSet<string> BannedSlugs = [];

    public ErrorOr<NormalizedTag> Normalize(string input)
    {
        var slug = ToSlug(input);

        if (slug.Length == 0)
            return TagErrors.Empty;

        if (slug.Length < TagLimits.MinSlugLength)
            return TagErrors.TooShort;

        if (slug.Length > TagLimits.MaxSlugLength)
            return TagErrors.TooLong;

        if (BannedSlugs.Contains(slug))
            return TagErrors.BannedWord;

        return new NormalizedTag(input.Trim(), slug);
    }

    public string ToSlug(string input)
    {
        // Decompose so diacritics become separate combining marks we can drop (café -> cafe).
        var decomposed = input.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);

        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                continue;

            if (character < 128 && char.IsLetterOrDigit(character))
                builder.Append(character);
            else if (char.IsWhiteSpace(character) || character is '-' or '_')
                builder.Append('-');
            // Everything else (non-ASCII letters, punctuation, symbols) is dropped.
        }

        return CollapseHyphens(builder.ToString()).Trim('-');
    }

    private static string CollapseHyphens(string value)
    {
        var builder = new StringBuilder(value.Length);
        var previousWasHyphen = false;

        foreach (var character in value)
        {
            if (character == '-')
            {
                if (previousWasHyphen)
                    continue;

                previousWasHyphen = true;
            }
            else
            {
                previousWasHyphen = false;
            }

            builder.Append(character);
        }

        return builder.ToString();
    }
}
