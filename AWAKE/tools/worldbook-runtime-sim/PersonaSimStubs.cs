using System.Text;

namespace Awake;

// 仅顶住 PersonaDslGenerator 依赖的最小替身（不引 TaleWorlds / MarcusAwakeFramework）。
// 只提供它真正用到的两个类型，其余一律不管。

internal sealed class NpcDialogueChatEntry
{
    internal string Role { get; }
    internal string Text { get; }

    internal NpcDialogueChatEntry(string role, string text)
    {
        Role = role ?? string.Empty;
        Text = text ?? string.Empty;
    }
}

internal static class NpcDialoguePromptPipeline
{
    // PersonaDslGenerator 在 DSL 超预算时调用此方法；此处与原实现一致地按 UTF-8 预算截断。
    internal static string EnsureBudget(string text, int budgetBytes)
    {
        if (string.IsNullOrEmpty(text) || budgetBytes <= 0) return string.Empty;
        if (Encoding.UTF8.GetByteCount(text) <= budgetBytes) return text;
        int bytes = 0;
        StringBuilder builder = new StringBuilder();
        var enumerator = System.Globalization.StringInfo.GetTextElementEnumerator(text);
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