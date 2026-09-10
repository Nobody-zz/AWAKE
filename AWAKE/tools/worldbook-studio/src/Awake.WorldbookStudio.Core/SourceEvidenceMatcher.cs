using System.Text;

namespace Awake.WorldbookStudio.Core;

internal readonly record struct SourceEvidenceMatch(
    int StartUtf16,
    int EndUtf16,
    string Quote,
    bool Exact);

internal static class SourceEvidenceMatcher
{
    public static bool AreEquivalent(string sourceText, string candidateText)
    {
        var source = NormalizeLineEndings(sourceText ?? string.Empty).Normalize(NormalizationForm.FormC);
        var candidate = PrepareCandidate(candidateText);
        if (source.Length == 0 || candidate.Length == 0)
            return false;

        return BuildIndex(source).Text == BuildIndex(candidate).Text;
    }

    public static bool TryFind(string sourceText, string candidateText, out SourceEvidenceMatch match)
    {
        var source = sourceText ?? string.Empty;
        var candidate = PrepareCandidate(candidateText);
        if (source.Length == 0 || candidate.Length == 0)
        {
            match = default;
            return false;
        }

        var exactStart = source.IndexOf(candidate, StringComparison.Ordinal);
        if (exactStart >= 0)
        {
            match = new SourceEvidenceMatch(exactStart, exactStart + candidate.Length, source[exactStart..(exactStart + candidate.Length)], true);
            return true;
        }

        var sourceIndex = BuildIndex(source);
        var candidateIndex = BuildIndex(candidate);
        if (candidateIndex.Text.Length == 0)
        {
            match = default;
            return false;
        }

        var canonicalStart = sourceIndex.Text.IndexOf(candidateIndex.Text, StringComparison.Ordinal);
        if (canonicalStart < 0)
        {
            match = default;
            return false;
        }

        var canonicalEnd = canonicalStart + candidateIndex.Text.Length - 1;
        var start = sourceIndex.Starts[canonicalStart];
        var end = sourceIndex.Ends[canonicalEnd];
        match = new SourceEvidenceMatch(start, end, source[start..end], false);
        return true;
    }

    private static string PrepareCandidate(string value)
    {
        var candidate = NormalizeLineEndings(value ?? string.Empty).Trim();
        if (candidate.StartsWith("```", StringComparison.Ordinal) && candidate.EndsWith("```", StringComparison.Ordinal) && candidate.Length >= 6)
            candidate = candidate[3..^3].Trim();

        while (candidate.Length >= 2 && IsOpeningQuote(candidate[0]) && IsClosingQuote(candidate[^1]))
            candidate = candidate[1..^1].Trim();

        return candidate;
    }

    private static CanonicalIndex BuildIndex(string value)
    {
        var text = new StringBuilder(value.Length);
        var starts = new List<int>(value.Length);
        var ends = new List<int>(value.Length);

        for (var index = 0; index < value.Length;)
        {
            var start = index;
            var consumed = index + 1 < value.Length && char.IsHighSurrogate(value[index]) && char.IsLowSurrogate(value[index + 1]) ? 2 : 1;
            var token = value.Substring(index, consumed).Normalize(NormalizationForm.FormKC);
            for (var tokenIndex = 0; tokenIndex < token.Length; tokenIndex++)
            {
                var character = ToCanonicalCharacter(token[tokenIndex]);
                if (char.IsWhiteSpace(character) || IsFormattingCharacter(character, value, start))
                    continue;

                text.Append(char.ToUpperInvariant(character));
                starts.Add(start);
                ends.Add(start + consumed);
            }

            index += consumed;
        }

        return new CanonicalIndex(text.ToString(), starts, ends);
    }

    private static char ToCanonicalCharacter(char character)
    {
        if (character is >= '\uFF01' and <= '\uFF5E')
            character = (char)(character - 0xFEE0);
        else if (character == '\u3000')
            character = ' ';

        return character switch
        {
            '，' or '、' => ',',
            '。' => '.',
            '！' => '!',
            '？' => '?',
            '：' => ':',
            '；' => ';',
            '（' => '(',
            '）' => ')',
            '【' => '[',
            '】' => ']',
            '「' or '『' => '"',
            '」' or '』' => '"',
            '“' or '”' => '"',
            '‘' or '’' => '\'',
            '—' or '–' or '−' or '－' => '-',
            _ => character
        };
    }

    private static bool IsFormattingCharacter(char character, string source, int index)
    {
        if (character is '*' or '_' or '`' or '~')
            return true;
        if (character != '#')
            return false;
        return index == 0 || source[index - 1] == '\n';
    }

    private static bool IsOpeningQuote(char character)
        => character is '"' or '\'' or '“' or '‘' or '「' or '『' or '`';

    private static bool IsClosingQuote(char character)
        => character is '"' or '\'' or '”' or '’' or '」' or '』' or '`';

    private static string NormalizeLineEndings(string value)
        => value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    private sealed record CanonicalIndex(string Text, IReadOnlyList<int> Starts, IReadOnlyList<int> Ends);
}
