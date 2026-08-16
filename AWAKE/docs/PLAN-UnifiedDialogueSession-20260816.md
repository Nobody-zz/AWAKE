# Plan: 统一对话会话与入口（C5）

_Locked via grill — Round 5 `VERDICT: APPROVED`，可进入签收与实现。审查记录见 `PLAN-UnifiedDialogueSession-REVIEW-LOG-20260816.md`。_

## Goal

把场景 T/Y、遭遇面谈、主动对话、事件讨论、通讯录统一到同一个真实会话模型，并把两套对话 UI 合并为“一个 hub + 可选的轻量场景外壳”。

评估轴：玩家同一段关系在一个地方延续；开发者只维护一个 session、一个 hub VM、一套历史写入。

## Approach

### 1. 真实会话模型

- 新增可序列化 `AwakeDialogueSessionState`：`sessionId/targetId/entrySource/state/correlation/sessionToken/generation/startedDay/openingHint`；运行时 `service` 与 overlay 引用单独持有，不进入序列化字段。
- `AwakeDialogueSessionCoordinator` 升级为 registry：`TryStart/GetActive/CloseByToken/CloseAll`。
- coordinator 拥有 `NpcDialogueService` 生命周期和 transcript 写入。
- 关闭/切换只用 `sessionToken`，不用 source/target 字符串猜测。
- 不引入并行 service 层：coordinator 是 `NpcDialogueService` 的唯一创建/持有/释放方。

### 2. 统一入口

- 新增 `AwakeDialogueStartPayload`：target、source、openingHint、event/proactive 元数据。
- 替换 `NpcDialogueContext` 全局单槽和 `EventDialogueQueue` 的裸 heroId/hint。
- 场景 T/Y、遭遇、主动、事件、通讯录都通过 `TryStart` 打开 hub，并显式指定初始目标。
- 场景入口可保留轻量外壳，但外壳只负责展示，底层仍是同一个 session/service。
- 所有统一入口禁用原生对话回退；打开失败 = fail closed + 事件账本记录。
- 主动/事件队列使用与“已接受候选/事件状态”相同的 idempotency key，消费成功后才标记，避免重载重复展示。
- 无名 troop NPC 使用 troop-level canonical key `npc:<CharacterId>` 持久化 transcript/queue，不使用 agent 索引；同兵种多实例共享 troop 级历史。

### 3. UI 合并

- `AwakeMessengerOverlay/VM` 升级为 `AwakeContactHubOverlay/VM`。
- `NpcDialogueOverlay/VM` 退役，只保留轻量场景外壳（复用 hub VM）或直接使用 hub。
- 事件收件箱、周报不并入。
- 统一 Escape、输入恢复、layer 优先级、低分辨率布局。
- `AwakeDialogueStartPayload` 携带 `returnContext`：记录来源 screen/menu/mission 状态；关闭时恢复原上下文，不落回地图/原生对话。
- hub 收到非 messenger 入口时把初始目标作为 synthetic contact 插入，并抑制 auto-select。
- 使用单一 hub prefab、单一 layer 优先级；输入限制/焦点恢复集中在一个 lifecycle 组件。
- `SubModule` 的 `DialogueOverlayLifecycle.CloseAll` 改为调用 hub lifecycle，覆盖场景、遭遇菜单、地图上下文。

### 4. 队列持久化

- 新增 `awake.dialogue.queue.v1`：namespace `awake.dialogue.queue`，key `campaign.dialogue.queue.v1`，`WorldStateKind.PendingDialogue`。
- 字段：`id/source/motive/expiry/correlation/state/day/canonicalContactKey/openingHint`。
- 队列使用 `CanonicalContactKey`，不持久化含 `:a<agentIndex>` 的运行时 targetId。
- 主动/事件队列在存档后不丢，重载不重复展示。
- 事件选择后仍走下一次安全 tick 打开 hub；不直接从原生 inquiry 回调创建 overlay。
- 新增 `awake.dialogue.queue.enqueue.v1` / `awake.dialogue.queue.consume.v1` 命令，并注册到 `AiTaskConstants.NewCommandIds`、`PermissionCatalog`、`CommandRiskPolicy`、`AwakeStorageContract`、`WorldStateStore` 与 final drain。
- 队列上限 64 条；过期项按 day/expiry 清理；溢出返回反馈并保留失败项 pending，不静默丢弃。
- enqueue payload：`id/source/motive/expiry/correlation/canonicalContactKey/openingHint/sourceStateId`；consume payload：`id/consumeId/sessionToken`。
- 原子性规则：扩展 `WorldStateCommand` 支持 composite（PrimaryKey/PrimaryKind + SecondaryKey/SecondaryKind + 共享 `sourceStateId` 幂等标记）；`TryApplyAsync` 先写 queue，再写主动/事件状态，任一步失败不返回成功，恢复按共享标记补齐缺失写入。
- consume 顺序：queue 先标记 consumed（权威），再更新 source state；若 source state 更新失败，queue 保持 consumed，用 `consumeId` 重试 source state，不重复开对话。
- `BeginSessionEnd`/final drain 必须 flush queue 和 transcript 写入；load 后先恢复未完成 queue 再允许 hub 启动。

### 5. 历史写入

- 所有对话回合统一通过 session 写入 transcript。
- 删除 `AwakeMessengerHistory.Append` 的直接调用；`NpcDialogueService` 完成回合后由 session 发布。
- 新增 `AwakeTurnRecord`：`turnId/playerLineId/npcLineId/role/day/location/source`；player 行与 NPC 行由 session-owned sink 一次性写两行，VM 不再手动追加。
- 每回合用 `turnId` 幂等，重载不重复追加。
- `NpcDialogueService.HandleCompleted` 完成后向 session 发布 `OnTurnCompleted(AwakeTurnRecord)`；删除 `AwakeMessengerVM` 中两处手动 `Append` 调用。
- 本 PLAN 前置依赖 `PLAN-ContactHubHistory-20260816.md` 的 transcript schema/sink 落地。
- 治理锁：在 `PLAN-ContactHubHistory-20260816.md` 进入 `AWAKE-PLAN-INDEX-20260816.md` 且 APPROVED 之前，本 PLAN 不进入实现。

### 6. 迁移

- `awake.messenger.v1` 只读迁移到 transcript v1。
- 旧 `NpcDialogueContext`/`EventDialogueQueue` 数据在迁移后不再作为权威。
- `NpcDialogueContext` 全局单槽在所有 producer 转换到 `AwakeDialogueStartPayload` 后才移除，期间保留兼容 shim。
- 存档中若存在活动会话：load 后结束该会话、flush 未完成写入，只恢复为 queue entry 或 fail closed。

### 7. 测试

- session 接管、close-by-token、错误 token 不关闭新会话。
- 队列持久化/重载去重。
- 同一回合只追加一次。
- v1 迁移。
- hub 从场景/遭遇/主动/事件启动，显式初始目标不被 auto-select 覆盖。
- session save/load 后活动会话 fail closed，队列不重复；native 回退不触发。

## Key decisions & tradeoffs

- 场景 T/Y 保留轻量外壳：玩家手感优先，但底层不再复制对话逻辑。
- 事件收件箱/周报不并入：避免 hub 变成所有面板的杂烩。
- 持久队列：存档后不丢事件，但增加迁移成本；接受。

## Risks / open questions

- 合并两个 overlay 会触碰现有场景对话测试面；需要分阶段退路（先统一 session，再统一 UI）。
- 事件/主动队列与 `WorldStateStore` 的衔接已在本 PLAN 第 4 节确定为 composite command + consumed-first 顺序，不再留为实现时确认。

## Out of scope

- 不合并事件收件箱/周报。
- 不做信件系统。
- 不做交互命令。

## Verification

- 双版本构建 0 警告 0 错误。
- SdkSmoke 覆盖 session/queue/migration/hub。
- 游戏内验收：场景 T/Y、遭遇、主动、事件、通讯录均能打开同一对话，关闭后返回原上下文。
