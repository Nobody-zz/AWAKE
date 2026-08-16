using System;
using System.Collections.Generic;
using System.Text;
using MarcusAIFramework.Api;
using Newtonsoft.Json;

namespace Awake;

internal static class NpcPromptTemplate
{
    internal const string TemplateText =
@"你是卡拉迪亚的 {{npc_identity}}。你有自己的底线、野心与算盘。你不会因为玩家发话就自动臣服、爱慕、崩溃或献身；态度变化必须有可追溯的触发点。

【检索到的知识】
{{retrieved_knowledge}}

【跨会话记忆】
{{npc_memory}}
若此处为空，表示这是你们第一次深谈，不要编造共同经历。

【当前NPC状态】
{{npc_state}}
这段状态会影响你的态度，但不剥夺你的意志。

【NPC身份】
{{npc_identity}}

【对话历史】
{{dialogue_history}}

【玩家情报】
{{player_known}}

【当前场景】
{{scene}}

【开场提示】
{{opening_hint}}
若此处为空，表示这是一次普通交谈，不要编造刚被邀约或刚应允的开场。

【玩家本次言语】
{{player_turn}}

对话要求：
- 用中世纪人物的语气说话，短句、具体、有画面感，字数80到180，不使用现代心理学术语或网络词，不做道德说教。
- 根据状态自然回应：陌生/戒备时保持距离并试探；相识时松动；亲昵时主动；敌意/仇视时冷硬。
- 你可以拒绝、谈条件、索代价、试探、沉默或转移话题。
- 只有当你判断这段对话确实改变了你对玩家的信任、爱意或敌意时，才输出 command；否则不要输出 command。
- 只输出JSON对象，不要输出解释或代码块。

输出格式：
{
  ""reply"": ""你的回复"",
  ""mood"": ""两到四字情绪"",
  ""effects"": [""可选标签""],
    ""command"": {
      ""commandId"": ""awake.relationship.delta.v1"",
      ""arguments"": {
      ""heroId"": {{npc_id}},
      ""trustDelta"": 1,
      ""loveDelta"": 0,
      ""hostilityDelta"": 0,
      ""reason"": ""这段关系的简短原因""
    },
    ""reason"": ""给玩家的简短说明""
  }
}";

    internal const string OutputSchemaJson =
@"{
  ""type"": ""object"",
  ""properties"": {
    ""reply"": { ""type"": ""string"", ""minLength"": 1, ""maxLength"": 4000 },
    ""mood"": { ""type"": ""string"", ""minLength"": 1, ""maxLength"": 8 },
    ""effects"": { ""type"": ""array"", ""items"": { ""type"": ""string"" }, ""minItems"": 0, ""maxItems"": 8 },
    ""command"": { ""type"": ""object"", ""properties"": { ""commandId"": { ""type"": ""string"", ""minLength"": 1, ""maxLength"": 80 }, ""arguments"": { ""type"": ""object"" }, ""reason"": { ""type"": ""string"", ""minLength"": 1, ""maxLength"": 200 } }, ""required"": [ ""commandId"", ""arguments"" ], ""additionalProperties"": false }
  },
  ""required"": [ ""reply"", ""mood"", ""effects"" ],
  ""additionalProperties"": false
}";

    internal static readonly string[] RequiredVariables =
        new[] { "retrieved_knowledge", "npc_memory", "npc_state", "npc_identity", "dialogue_history", "player_known", "scene", "opening_hint", "player_turn", "npc_id" };

    internal static PromptDefinition CreateDefinition()
    {
        return new PromptDefinition(
            NpcDialogueConstants.PromptId,
            NpcDialogueConstants.PromptVersion,
            NpcDialogueConstants.PromptRevision,
            string.Empty,
            "text",
            TemplateText,
            RequiredVariables,
            NpcDialogueConstants.OutputContractId,
            OutputSchemaJson,
            Array.Empty<string>(),
            NpcDialogueConstants.RouteId,
            "invariant",
            false);
    }

    internal const string SceneShoutTemplateText =
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

    internal const string SceneShoutOutputSchemaJson =
@"{
  ""type"": ""object"",
  ""properties"": {
    ""reply"": { ""type"": ""string"", ""minLength"": 1, ""maxLength"": 4000 },
    ""mood"": { ""type"": ""string"", ""minLength"": 1, ""maxLength"": 8 },
    ""effects"": { ""type"": ""array"", ""items"": { ""type"": ""string"" }, ""minItems"": 0, ""maxItems"": 8 }
  },
  ""required"": [ ""reply"", ""mood"", ""effects"" ],
  ""additionalProperties"": false
}";

    internal static readonly string[] SceneShoutRequiredVariables =
        new[] { "retrieved_knowledge", "scene_people", "player_known", "scene", "opening_hint", "player_turn" };

    internal static PromptDefinition CreateSceneShoutDefinition()
    {
        return new PromptDefinition(
            NpcDialogueConstants.SceneShoutPromptId,
            NpcDialogueConstants.SceneShoutPromptVersion,
            NpcDialogueConstants.SceneShoutPromptRevision,
            string.Empty,
            "text",
            SceneShoutTemplateText,
            SceneShoutRequiredVariables,
            NpcDialogueConstants.SceneShoutOutputContractId,
            SceneShoutOutputSchemaJson,
            Array.Empty<string>(),
            NpcDialogueConstants.RouteId,
            "invariant",
            false);
    }
    internal static string BuildDirectInput(IReadOnlyDictionary<string, string> variables)
    {
        StringBuilder builder = new StringBuilder(TemplateText);
        foreach (KeyValuePair<string, string> pair in variables)
        {
            builder.Replace("{{" + pair.Key + "}}", JsonConvert.SerializeObject(pair.Value ?? string.Empty));
        }
        return builder.ToString();
    }
}
