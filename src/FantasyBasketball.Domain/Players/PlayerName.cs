using System.Globalization;
using System.Text;

namespace FantasyBasketball.Domain.Players;

public static class PlayerName
{
    private static readonly HashSet<string> Suffixes =
        new(StringComparer.Ordinal) { "jr", "sr", "ii", "iii", "iv", "v" };

    public static string Normalize(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var decomposed = value.Normalize(NormalizationForm.FormD);
        var withoutMarks = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category is not UnicodeCategory.NonSpacingMark
                and not UnicodeCategory.SpacingCombiningMark
                and not UnicodeCategory.EnclosingMark)
            {
                withoutMarks.Append(char.ToLowerInvariant(character));
            }
        }

        var withoutPunctuation = withoutMarks
            .Replace(".", string.Empty)
            .Replace("'", string.Empty)
            .Replace("’", string.Empty)
            .Replace("-", string.Empty)
            .Replace(",", string.Empty)
            .ToString();
        var tokens = withoutPunctuation.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (tokens.Length > 1 && Suffixes.Contains(tokens[^1]))
        {
            tokens = tokens[..^1];
        }

        var result = string.Join(' ', tokens);
        if (string.IsNullOrWhiteSpace(result))
        {
            throw new ArgumentException(
                "Player name must contain letters or digits after normalization.",
                nameof(value));
        }

        return result;
    }
}
