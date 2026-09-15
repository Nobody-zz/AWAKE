using System;
using System.Text;
using MarcusAwakeFramework.Api;
using Newtonsoft.Json.Linq;

namespace Awake;

/// <summary>
/// NPC 跨会话记忆摘要的内置提示词、输入组装和输出解析。
/// 与 NpcMemoryService 的调度、存储和重试逻辑分离，便于单独维护提示词。
/// </summary>
internal static class NpcMemorySummaryTemplate
{
    internal const string TemplateText = "{{input}}";

    internal const string OutputSchemaJson =
@"{
  ""type"": ""object"",
  ""properties"": {
    ""summary"": { ""type"": ""string"", ""minLength"": 1, ""maxLength"": 240 }
  },
  ""required"": [ ""summary"" ],
  ""additionalProperties"": false
}";

    internal static PromptDefinition CreateDefinition()
    {
        return new PromptDefinition(
            NpcMemoryConstants.PromptId,
            NpcMemoryConstants.PromptVersion,
            NpcMemoryConstants.PromptRevision,
            string.Empty,
            "text",
            TemplateText,
            new[] { "input" },
            NpcMemoryConstants.OutputContractId,
            OutputSchemaJson,
            Array.Empty<string>(),
            NpcMemoryConstants.RouteId,
            "invariant",
            false);
    }

    internal static string BuildInput(string heroId, JArray facts, string summaryHint)
    {
        string factText = facts == null ? string.Empty : string.Join("、", facts);
        string hint = AwakeRuntime.TruncateTextElements(summaryHint ?? string.Empty, 400);
        StringBuilder builder = new StringBuilder();
        builder.Append("你是卡拉迪亚的记忆书记官，任务是把玩家与 NPC（" + heroId + "）之间最近一次交谈压缩成一句跨会话记忆摘要。");
        builder.Append("只依据下面提供的事实与对话提示，不编造、不评价、不使用现代词汇。");
        builder.Append("输出必须是 JSON 对象，只含一个字段：{\"summary\": \"...\"}，summary 不超过 240 字。");
        builder.Append("\n事实：").Append(string.IsNullOrWhiteSpace(factText) ? "无" : factText);
        builder.Append("\n对话提示：").Append(string.IsNullOrWhiteSpace(hint) ? "无" : hint);
        return builder.ToString();
    }

    internal static string ParseSummary(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        try
        {
            JObject root = JObject.Parse(text);
            string summary = (string)root["summary"];
            return AwakeRuntime.TruncateTextElements(summary ?? string.Empty, NpcMemoryConstants.SummaryMaximumChars);
        }
        catch
        {
            return string.Empty;
        }
    }
}
