# Batch A 审查日志

> 审查对象：对话入口与 UI 边界
> 日期：2026-08-16
> 状态：Round 1 REVISE

## Round 1 - Codex 对抗审查

### 发现 1：UI 路由没有锁死

计划只写了边界，但没有明确“哪个入口打开哪个 UI”。现状是：

- 场景 T/Y、遭遇面谈 -> `NpcDialogueOverlay`
- 通讯录 -> `AwakeMessengerOverlay`

如果后续有人把入口接到 Messenger，就会导致场景对话脱离原版感。

修订：增加入口 -> UI 路由表。

### 发现 2：遭遇面谈缺少敌意门

当前计划没有说明是否在敌对遭遇、攻城、劫掠中显示“面谈”。如果不加门，玩家可能在战斗中打开 AI 面谈，破坏上下文。

修订：`PlayerEncounterState` 只允许 `Begin / Wait`，且不在战斗/攻城/劫掠上下文中显示。

### 发现 3：T/Y 与命令台快捷键冲突

`Y` 同时是命令台默认快捷键，也是场景确认键。当前实现靠“场景优先”解决，但计划没有写清楚。

修订：锁定“场景内 Y=确认，场景外 Y=命令台”；后续再做 MCM 分键配置。

### 发现 4：场景候选过滤不完整

计划没有规定场景候选是否排除敌对阵营、未成年、非活动 Agent。

修订：只允许成年、活动、非敌对阵营 Agent；战斗/竞技场/决斗等模式不触发。

### 发现 5：Messenger 计划文案有冲突

`PLAN-Awake-Messenger` 里写过“所有入口都进入同一套会话视图”，这和“原版对话 UI 保留”冲突。

修订：统一为“远程/异步通信进 Messenger，场景/面谈 AI 对话仍走 `NpcDialogueOverlay`”。

## 结论

`VERDICT: REVISE`

修订后的锁定决策写入 `PLAN-Awake-Dialogue-BatchA-20260816.md`。
