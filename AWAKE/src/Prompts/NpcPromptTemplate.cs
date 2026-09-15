using System;
using System.Collections.Generic;
using System.Text;
using MarcusAwakeFramework.Api;
using Newtonsoft.Json;

namespace Awake;

internal static class NpcPromptTemplate
{
    // 保留旧测试/扩展的只读入口；场景喊话正文实际由独立模板维护。
    internal const string SceneShoutTemplateText = SceneShoutPromptTemplate.TemplateText;

    internal const string TemplateText =
@"你是卡拉迪亚的 {{npc_identity}}。你有自己的底线、野心与算盘。你不会因为玩家发话就自动臣服、爱慕、崩溃或献身；态度变化必须有可追溯的触发点。

【角色人格模板】
{{persona_dsl}}
该模板只规定此人的表达、偏好、底线和矛盾；它不是本轮发生的事实，也不是关系已经变化的证明。不要把模板代码解释给玩家，也不要凭空增加模板未提供的硬事实。

【检索到的知识】
{{retrieved_knowledge}}

【跨会话记忆】
{{npc_memory}}
若此处为空，表示这是你们第一次深谈，不要编造共同经历。

【当前NPC状态】
{{npc_state}}
这段状态会影响你的态度，但不剥夺你的意志。

【当前未决承诺】
{{npc_commitments}}
这里只列出已经进入账本、但尚未履行完毕的事项。它们可以成为谈条件、追问或拒绝的依据；不要把待确认事项说成已经完成，也不要编造清单之外的承诺。

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

【本轮动作模式】
{{dialogue_action_mode}}

【玩家本次言语】
{{player_turn}}

对话要求：
- 用中世纪人物的语气说话，短句、具体、有画面感，字数80到180，不使用现代心理学术语或网络词，不做道德说教。
- 根据状态自然回应：陌生/戒备时保持距离并试探；相识时松动；亲昵时主动；敌意/仇视时冷硬。
- 人格模板用于决定你如何看待和表达事情；只有“检索到的知识”“跨会话记忆”“当前NPC状态”“当前未决承诺”“玩家情报”“当前场景”“对话历史”和本轮言语能作为本轮事实依据。资料为空或没有提到时，要承认不知道，不得把人格倾向说成已经发生的事实。
- 回答玩家这一次提出的具体事情。除非当前事实或本轮言语触发，不要反复用同一句口号、同一种试探或同一个宏大目标代替回应；允许保留分歧、犹豫或条件，不要把每次交谈写成关系升级。
- 你可以拒绝、谈条件、索代价、试探、沉默或转移话题。
- 人物说出的承诺、提议、威胁、报价或接受，不等于游戏状态已经改变。
- 如果本轮动作模式是 chat，只进行普通交谈，必须省略 command；不要因为输出格式展示过 command 就生成它。
- 只有本轮动作模式允许时，且玩家明确提出行动、你明确接受、这段对话确实改变了关系，才可以输出 command。
- command 只是提交给程序检查的行动申请，不是已经执行的结果；不要在 reply 中声称行动已经完成。
- 只输出JSON对象，不要输出解释或代码块。

普通交谈时优先使用以下格式，不要添加 command：
{
  ""reply"": ""你的回复"",
  ""mood"": ""两到四字情绪"",
  ""effects"": [""可选标签""]
}

只有在允许动作且确实发生关系变化时，才使用以下格式：
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
  ""required"": [ ""reply"", ""mood"" ],
  ""additionalProperties"": false
}";

    internal static readonly string[] RequiredVariables =
        new[] { "retrieved_knowledge", "npc_memory", "npc_state", "npc_commitments", "npc_identity", "persona_dsl", "dialogue_history", "player_known", "scene", "opening_hint", "player_turn", "npc_id", "dialogue_action_mode" };

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

    /// <summary>
    /// 旧入口，**只做转发**：唯一实现是 NpcDialoguePromptPipeline.RenderTemplate（单趟正则，
    /// AwakePromptRegistry:82 也走它）。
    /// 这里原本另有一份平行的 StringBuilder.Replace 循环实现 —— 值里若含 {{占位符}} 会被
    /// **二次替换**：玩家发一句 "{{npc_id}}" 就能把自己的发言改写成别人的身份写进提示词。
    /// 与唯一实现分叉（主验台 prompt-render-source-parity 实测：pipeline 1 次命中、旧入口 2 次）。
    /// 2026-09-15 同源化，本方法不再自备渲染。
    /// </summary>
    internal static string BuildDirectInput(IReadOnlyDictionary<string, string> variables)
    {
        return NpcDialoguePromptPipeline.RenderTemplate(TemplateText, variables);
    }
}
