# AWAKE 存储与 NPC 记忆游戏内验收清单

> 日期：2026-08-16
> 状态：SdkSmoke 已覆盖离线逻辑，以下项目需要进游戏验证。

## 1. 存储管道

检查点：

- 进入战役后确认日志出现 `world_state_namespace_open_degraded` 或成功打开：
  - `awake.npc.memories`
  - `awake.event_meta`
  - `awake.relationships`
- 与 NPC 深谈并让 AI 输出关系命令后，确认日志出现 `world_command_result` 且 `ok=True`。
- 关闭对话后重新进入同一 NPC，确认 `npc_state` 显示新的信任/爱意/敌意。
- 保存并读档，再次进入同一 NPC，确认关系数值仍然保留。
- 触发过一次事件后，保存并读档，确认事件冷却不会被重置。

通过标准：

- 三个 namespace 均可读写。
- 关系状态读档后仍存在。
- 事件冷却读档后仍存在。
- 日志无 `world_state_write_failed_dropped`。

## 2. NPC 记忆

检查点：

- 第一次深谈时，AI 不应编造共同经历。
- 对话中有实质内容后关闭对话，日志出现 `npc_memory_facts_flush` 或等价成功记录。
- 再次进入同一 NPC，确认提示词 `npc_memory` 非空，且包含上次事实或摘要。
- 保存并读档后再次进入，确认记忆仍可读回。
- 对话无实质内容时关闭，不应生成空记忆条目。

通过标准：

- 跨会话记忆可读回。
- 摘要 best-effort 失败时至少保留事实账。
- 读档后记忆不重复膨胀。

## 3. 日志位置

- `Modules/AWAKE/Logs/Awake.log`
- `Modules/MarcusAIFramework/log/framework.log`

## 4. 当前结论

- 离线：存储 roundtrip、drain、幂等、记忆 top-k、事实上限、摘要解析均已由 SdkSmoke 覆盖。
- 待验收：Companion 真实存储、读档持久化、NPC 对话记忆回读。
