# AWAKE · AF Batch 1 落地方案

> 日期：2026-08-16
> 范围：A1 NPC 主动聊天状态机 + A4 覆盖层长等待解锁
> 依据：`docs/AF-Structures-Landing-Plan-20260816.md`
> 原则：环境本地化、代码自建、不复制 AF 实现。

## 1. 目标

- 建立可持久化的 NPC 主动聊天状态机。
- 对话覆盖层在 AI 等待超过 60 秒后允许 Esc 取消生成并关闭。

## 2. A1 设计

### 2.1 数据

新增 `awake.npc.proactive` 存储 namespace，文档结构：

```json
{
  "schema": "awake.npc.proactive.v1",
  "updatedUtc": "...",
  "candidates": [],
  "appliedKeys": []
}
```

候选条目：

```json
{
  "heroId": "...",
  "motive": "casual",
  "urgency": 1,
  "affinity": 0,
  "state": "pending",
  "day": 10,
  "expiresAtDay": 11,
  "cooldownDay": 12,
  "fatigue": 1,
  "openingHint": "..."
}
```

状态：`pending` → `opening` → `accepted` / `rejected`；过期后清理。

### 2.2 触发

- 每游戏小时由 `AwakeEventBehavior` 调 `NpcProactiveService.OnHourlyTickAsync`。
- 候选池：`NpcDialogueLauncher.GetNearbyHeroes(8)`，只取当前场合真正能交谈的英雄。
- 动机：`casual`、`relationship`、`party_wounded`，后续可扩展。
- 冷却：英雄冷却按游戏日；疲劳累积到上限后暂停。
- 每次最多评估 8 人，只创建一个 pending。

### 2.3 弹窗

- `OnApplicationTick` 消费 pending，弹出 `InformationManager.ShowInquiry`。
- 接受：写入 `NpcDialogueContext` + `EventDialogueQueue`，状态转 `accepted`。
- 拒绝：状态转 `rejected`，冷却 1 天。
- 弹窗期间状态先转 `opening`，防止重复弹窗。

### 2.4 持久化

- `WorldStateStore` 新增 `WorldStateKind.Proactive`。
- 读写走 Marcus Storage 的 `awake.npc.proactive` namespace。
- 不可用时降级为内存态，不阻塞游戏。

## 3. A4 设计

### 3.1 生成状态

- `NpcDialogueService` 新增 `IsSending` 与 `WaitingSinceUtc`。
- 提交成功后记录等待开始时间；完成/失败/取消时清空。
- `CanEscCancel` = 正在发送且等待超过 60 秒。

### 3.2 覆盖层 Esc

- 空闲或已结束时：Esc 立即关闭。
- 等待超过 60 秒：Esc 取消路由并关闭。
- 等待未满 60 秒：Esc 不关闭，显示“仍在回应”提示。

## 4. 本地化

新增 key：

- `awake.proactive.popup.title`
- `awake.proactive.popup.text`
- `awake.proactive.accept`
- `awake.proactive.decline`
- `awake.dialogue.long_wait_hint`

中英文都维护，回退文案与 key 同步。

## 5. 文件

- 新增 `NpcProactiveService.cs`
- 新增 `NpcProactiveModels.cs`
- 修改 `AiTaskConstants.cs`
- 修改 `WorldStateStore.cs`
- 修改 `AwakeEventBehavior.cs`
- 修改 `SubModule.cs`
- 修改 `ProbeExtension.cs`
- 修改 `NpcDialogueService.cs`
- 修改 `NpcDialogueOverlay.cs`
- 修改 `NpcDialogueVM.cs`
- 修改 `NpcDialogueConstants.cs`
- 修改中英文本地化 XML
- 修改 `AWAKE.Tests/Program.cs`

## 6. 测试

- 状态机：创建、pending→opening→accepted/rejected、过期清理、疲劳上限。
- 持久化：`WorldStateStore` proactive roundtrip、幂等、降级。
- 冷却：未到冷却日不评估。
- 长等待：边界 59/60/61 秒、空闲 Esc、发送中未满 60 秒不关。
- 本地化：key 存在且中英文一致。

## 7. 验收

- 构建 0 警告 / 0 错误。
- SdkSmoke PASS。
- 游戏内：NPC 主动弹窗 → 接受 → 深谈；拒绝后不再弹；AI 等待超 60 秒可 Esc。

## 8. 不做

- 不实现完整动机疲劳表、队伍快照、事件动机。
- 不做周报/收件箱。
- 不引入 AF 代码。
