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

    /// <summary>
    /// 开发者诊断只保留最近一次直接对话的上下文来源是否到位；不保存人物正文、记忆或玩家输入。
    /// </summary>
    internal static IReadOnlyList<KeyValuePair<string, string>> BuildContextDiagnosticRows()
    {
        return NpcDialogueContextDiagnostics.Snapshot();
    }

    internal static void RecordContextDiagnostics(
        string heroId,
        bool sceneShout,
        IReadOnlyDictionary<string, string> variables)
    {
        NpcDialogueContextDiagnostics.Record(heroId, sceneShout, variables);
    }

    private static string BuildDirect(string template, Dictionary<string, string> variables)
    {
        return RenderTemplate(template, variables);
    }

    /// <summary>
    /// 模板渲染的唯一实现：{{key}} 替换为 JSON 字符串字面量，未提供的占位符原样保留。
    /// 本地提示词注册表的 CompileAsync 也必须走这里，避免两条渲染路径漂移。
    /// </summary>
    internal static string RenderTemplate(string template, IReadOnlyDictionary<string, string> variables)
    {
        string result = template ?? string.Empty;
        return PlaceholderPattern.Replace(result, match =>
        {
            string key = match.Groups[1].Value;
            string value;
            if (variables == null || !variables.TryGetValue(key, out value)) return match.Value;
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

internal static class NpcDialogueContextDiagnostics
{
    private static readonly object Gate = new object();
    private static List<KeyValuePair<string, string>> _latest = new List<KeyValuePair<string, string>>();

    internal static void Record(string heroId, bool sceneShout, IReadOnlyDictionary<string, string> variables)
    {
        List<KeyValuePair<string, string>> rows = new List<KeyValuePair<string, string>>
        {
            Row("dialogue_context.target", string.IsNullOrWhiteSpace(heroId) ? "unknown" : heroId),
            Row("dialogue_context.kind", sceneShout ? "scene_shout" : "npc_dialogue"),
            Row("dialogue_context.persona", Present(variables, "persona_dsl")),
            Row("dialogue_context.knowledge", Present(variables, "retrieved_knowledge")),
            Row("dialogue_context.memory", Present(variables, "npc_memory")),
            Row("dialogue_context.state", Present(variables, "npc_state")),
            Row("dialogue_context.commitments", Present(variables, "npc_commitments")),
            Row("dialogue_context.player_known", Present(variables, "player_known")),
            Row("dialogue_context.scene", Present(variables, "scene")),
            Row("dialogue_context.mode", Value(variables, "dialogue_action_mode"))
        };
        lock (Gate)
        {
            _latest = rows;
        }
    }

    internal static IReadOnlyList<KeyValuePair<string, string>> Snapshot()
    {
        lock (Gate)
        {
            return new List<KeyValuePair<string, string>>(_latest);
        }
    }

    private static KeyValuePair<string, string> Row(string key, string value)
    {
        return new KeyValuePair<string, string>(key, value ?? string.Empty);
    }

    private static string Present(IReadOnlyDictionary<string, string> variables, string key)
    {
        return string.IsNullOrWhiteSpace(Value(variables, key)) ? "absent" : "present";
    }

    private static string Value(IReadOnlyDictionary<string, string> variables, string key)
    {
        string value;
        return variables != null && variables.TryGetValue(key, out value) ? value ?? string.Empty : string.Empty;
    }
}
