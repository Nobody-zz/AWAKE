using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace Awake;

internal sealed class NpcPromptBoundedResult
{
    internal Dictionary<string, string> BoundedVariables { get; }
    internal string DirectText { get; }
    internal bool IsDirectOnly { get; }

    internal NpcPromptBoundedResult(Dictionary<string, string> boundedVariables, string directText, bool isDirectOnly = false)
    {
        BoundedVariables = boundedVariables ?? new Dictionary<string, string>(StringComparer.Ordinal);
        DirectText = directText ?? string.Empty;
        IsDirectOnly = isDirectOnly;
    }
}

internal static class NpcDialoguePromptPipeline
{
    internal const int MaximumPasses = 6;
    private static readonly Regex PlaceholderPattern = new Regex(
        "\\{\\{([A-Za-z0-9_]+)\\}\\}",
        RegexOptions.Compiled);
    private static readonly string[] TruncationOrder = new[]
    {
        "player_turn",
        "dialogue_history",
        "npc_memory",
        "retrieved_knowledge",
        "opening_hint",
        "npc_state"
    };

    internal static NpcPromptBoundedResult BuildBounded(
        Dictionary<string, string> rawVariables,
        IReadOnlyList<NpcDialogueChatEntry> history,
        string template,
        int budgetBytes)
    {
        Dictionary<string, string> variables = new Dictionary<string, string>(StringComparer.Ordinal);
        if (rawVariables != null)
        {
            foreach (KeyValuePair<string, string> pair in rawVariables)
            {
                variables[pair.Key] = pair.Value ?? string.Empty;
            }
        }
        variables["dialogue_history"] = SerializeHistory(history);

        string direct = BuildDirect(template, variables);
        if (Encoding.UTF8.GetByteCount(direct) <= budgetBytes)
        {
            return new NpcPromptBoundedResult(variables, direct);
        }

        for (int pass = 0; pass < MaximumPasses; pass++)
        {
            bool changed = false;
            foreach (string key in TruncationOrder)
            {
                string value;
                if (!variables.TryGetValue(key, out value) || string.IsNullOrEmpty(value)) continue;
                int currentBytes = Encoding.UTF8.GetByteCount(value);
                if (currentBytes <= 0) continue;
                int target = Math.Max(1, (currentBytes * 3) / 4);
                string truncated = TruncateUtf8(value, target);
                if (!StringComparer.Ordinal.Equals(truncated, value))
                {
                    variables[key] = truncated;
                    changed = true;
                }
            }
            direct = BuildDirect(template, variables);
            if (Encoding.UTF8.GetByteCount(direct) <= budgetBytes) break;
            if (!changed) break;
        }
        if (Encoding.UTF8.GetByteCount(direct) > budgetBytes)
        {
            return new NpcPromptBoundedResult(null, EnsureBudget(direct, budgetBytes), isDirectOnly: true);
        }
        return new NpcPromptBoundedResult(variables, direct);
    }

    internal static string EnsureBudget(string text, int budgetBytes)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        if (Encoding.UTF8.GetByteCount(text) <= budgetBytes) return text;
        return TruncateUtf8(text, budgetBytes);
    }

    private static string BuildDirect(string template, Dictionary<string, string> variables)
    {
        string result = template ?? string.Empty;
        return PlaceholderPattern.Replace(result, match =>
        {
            string key = match.Groups[1].Value;
            string value;
            if (!variables.TryGetValue(key, out value)) return match.Value;
            return JsonConvert.SerializeObject(value ?? string.Empty);
        });
    }

    private static string SerializeHistory(IReadOnlyList<NpcDialogueChatEntry> history)
    {
        StringBuilder builder = new StringBuilder();
        if (history == null) return builder.ToString();
        int count = 0;
        foreach (NpcDialogueChatEntry entry in history)
        {
            if (count >= NpcDialogueConstants.HistoryCapacity) break;
            string role = entry == null ? "npc" : entry.Role;
            string text = AwakeRuntime.TruncateTextElements(entry == null ? string.Empty : entry.Text, 400);
            if (builder.Length > 0) builder.Append('\n');
            builder.Append(role).Append("：").Append(text);
            count++;
        }
        return builder.ToString();
    }

    private static string TruncateUtf8(string value, int budgetBytes)
    {
        if (string.IsNullOrEmpty(value) || budgetBytes <= 0) return string.Empty;
        int bytes = 0;
        StringBuilder builder = new StringBuilder();
        TextElementEnumerator enumerator = StringInfo.GetTextElementEnumerator(value);
        while (enumerator.MoveNext())
        {
            string element = enumerator.GetTextElement();
            int next = Encoding.UTF8.GetByteCount(element);
            if (bytes + next > budgetBytes) break;
            builder.Append(element);
            bytes += next;
        }
        return builder.ToString();
    }
}
