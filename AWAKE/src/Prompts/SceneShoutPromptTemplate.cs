using System;
using MarcusAwakeFramework.Api;

namespace Awake;

/// <summary>
/// 场景喊话提示词及其输出契约。
/// 场景回应不代表固定 NPC，也不允许产生个人关系 command。
/// </summary>
internal static class SceneShoutPromptTemplate
{
    internal const string TemplateText =
@"你是卡拉迪亚场景中会回应玩家喊话的人们。你不是一个固定 NPC，而是由听见声音的在场者构成的声音。

【检索到的知识】
{{retrieved_knowledge}}

【在场人物】
{{scene_people}}
若此处为空，说明附近没有可辨认的人，但你仍可以作为一个场景声音回应。

【玩家情报】
{{player_known}}

【当前场景】
{{scene}}

【开场提示】
{{opening_hint}}

【玩家本次言语】
{{player_turn}}

对话要求：
- 用中世纪人物的语气回应，短句、具体、有画面感，字数60到160。
- 回应可以来自某个人、几个人交头接耳，或者场景里的集体反应；不要假装自己是某个具体英雄。
- 你可以拒绝、反问、起哄、沉默、转移话题，但不要自动服从。
- 场景喊话不结算任何个人关系，不输出 command。
- 只输出JSON对象，不要输出解释或代码块。

输出格式：
{
  ""reply"": ""回应"",
  ""mood"": ""两到四字情绪"",
  ""effects"": [""可选标签""]
}";

    internal const string OutputSchemaJson =
@"{
  ""type"": ""object"",
  ""properties"": {
    ""reply"": { ""type"": ""string"", ""minLength"": 1, ""maxLength"": 4000 },
    ""mood"": { ""type"": ""string"", ""minLength"": 1, ""maxLength"": 8 },
    ""effects"": { ""type"": ""array"", ""items"": { ""type"": ""string"" }, ""minItems"": 0, ""maxItems"": 8 }
  },
  ""required"": [ ""reply"", ""mood"" ],
  ""additionalProperties"": false
}";

    internal static readonly string[] RequiredVariables =
        new[] { "retrieved_knowledge", "scene_people", "player_known", "scene", "opening_hint", "player_turn" };

    internal static PromptDefinition CreateDefinition()
    {
        return new PromptDefinition(
            NpcDialogueConstants.SceneShoutPromptId,
            NpcDialogueConstants.SceneShoutPromptVersion,
            NpcDialogueConstants.SceneShoutPromptRevision,
            string.Empty,
            "text",
            TemplateText,
            RequiredVariables,
            NpcDialogueConstants.SceneShoutOutputContractId,
            OutputSchemaJson,
            Array.Empty<string>(),
            NpcDialogueConstants.RouteId,
            "invariant",
            false);
    }
}
