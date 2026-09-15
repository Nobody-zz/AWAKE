# AWAKE 内置提示词目录

本文档记录 AWAKE 运行时内置的模型提示词入口。它只记录运行时提示词的位置、用途和输出契约，不包含世界书正文或角色卡内容。

## 源码位置

所有内置提示词源码统一放在：

`AWAKE/src/Prompts/`

当前目录包含：

| 文件 | 用途 | Prompt ID | 输出契约 |
| --- | --- | --- | --- |
| `NpcPromptTemplate.cs` | 具体 NPC 与玩家的直接对话 | `awake.npc.v1` | `awake.npc.output.v1` |
| `SceneShoutPromptTemplate.cs` | 场景中的喊话回应，不绑定固定 NPC | `awake.scene_shout.v1` | `awake.scene_shout.output.v1` |
| `NpcMemorySummaryPrompt.cs` | 将一次 NPC 对话压缩为跨会话记忆摘要 | `awake.npc.memory.summary.v1` | `awake.npc.memory.summary.output.v1` |

## 维护规则

1. 新增或修改模型提示词，优先放入 `AWAKE/src/Prompts/`，不要把长提示词重新塞回业务服务类。
2. Prompt ID、版本、revision、输出契约和模板内容视为一个整体；修改输出字段时必须同步修改输出 schema 和对应测试。
3. 角色卡、世界书、周报和记忆内容是运行时变量或资料，不要复制到这个目录。
4. `command` 只是行动申请，不等于游戏状态已经改变；真正执行仍由程序的输出校验和命令治理负责。
5. 测试脚本如果需要读取真实提示词，必须引用 `AWAKE/src/Prompts/` 下的文件，不得另写一份近似模板。

## 调用关系

- `NpcDialogueService` 负责选择直接对话或场景喊话、注入变量、调用提示词登记/编译流程。
- `NpcMemoryService` 负责调度记忆摘要请求、重试和存储；提示词输入组装及输出解析位于 `NpcMemorySummaryPrompt.cs`。
- `AwakePromptRegistry` 只负责登记、按版本查找和渲染，不保存提示词正文的第二份副本。

人物直接对话有两个动作模式：`chat` 默认只生成回复，不保留 `command`；`negotiation` 允许生成行动申请，但仍须经过程序校验和命令治理。独立 NPC 对话面板和通讯录面板都提供模式切换；场景喊话始终禁止 `command`。

直接对话提示词还承担一条通用运行时规则：角色卡的 Persona DSL 只决定人物如何表达、取舍和保留矛盾，不能被当成本轮已发生的事实；当前事实只能来自知识、记忆、NPC 状态、已持久化的未决承诺、玩家情报、场景、对话历史和玩家本轮言语。未决承诺每轮从账本刷新，只包含待确认或已应允事项；已履行、拒绝或违约的历史由记忆层按需选取。开发者检查仅显示这些来源是否已进入最近一轮对话，不显示原文。
