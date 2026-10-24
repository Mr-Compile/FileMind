using System.Globalization;
using System.Text;

namespace FileMind.Domain.Classification;

public sealed record NormalizedTokens(string Original, IReadOnlyList<string> Tokens)
{
    public string Joined => string.Join(' ', Tokens);
}

public static class Normalizer
{
    public static NormalizedTokens Normalize(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return new NormalizedTokens(input, []);

        var normalized = input.Normalize(NormalizationForm.FormKC);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (c is '-' or '_' or '.' or ',' or '(' or ')' or '[' or ']' or '{' or '}')
                sb.Append(' ');
            else if (char.IsWhiteSpace(c))
                sb.Append(' ');
            else
                sb.Append(char.ToLower(c, CultureInfo.InvariantCulture));
        }

        var tokens = sb.ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return new NormalizedTokens(input, tokens);
    }

    public static bool Equivalent(string a, string b) =>
        Normalize(a).Joined == Normalize(b).Joined;
}
