using System;
using System.Text;

namespace Awake;

internal static class NpcDialogueReplyNormalizer
{
    internal static string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        StringBuilder builder = new StringBuilder();
        bool previousLineBreak = false;
        foreach (char c in text)
        {
            if (c == '\r')
            {
                continue;
            }
            if (c == '\n')
            {
                if (previousLineBreak) continue;
                previousLineBreak = true;
                builder.Append('\n');
                continue;
            }
            if (char.IsControl(c)) continue;
            previousLineBreak = false;
            builder.Append(c);
        }
        string result = builder.ToString().Trim();
        while (result.IndexOf("\n\n", StringComparison.Ordinal) >= 0)
        {
            result = result.Replace("\n\n", "\n");
        }
        return result;
    }
}
