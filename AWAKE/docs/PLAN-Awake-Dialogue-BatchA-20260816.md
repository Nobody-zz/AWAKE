# Batch A：对话入口与 UI 边界

> 日期：2026-08-16
> 状态：待用户签收的修订 PLAN
> 依据：`PLAN-Awake-Dialogue-GrillBatch-20260816.md` 与 Batch A 审查日志

## 1. 目标

锁定对话入口、UI 路由、场景选择交互和遭遇面谈的边界，不再出现“入口接错 UI”或“原版对话被替代”的问题。

## 2. 入口 -> UI 路由

| 入口 | UI | 说明 |
| --- | --- | --- |
| 场景 T/Y 选择 | `NpcDialogueOverlay` | 场景内 AI 对话，贴近原版对话覆盖层 |
| 遭遇面谈 | `NpcDialogueOverlay` | 地图遭遇菜单并行入口 |
| 通讯录深谈 | `AwakeMessengerOverlay` | 联系人会话 |
| 远程写信 / NPC 来信 | `AwakeMessengerOverlay` | 后续 M3 内嵌 |
| 原版对话 | 原版 UI | 永远保留，不替换 |

## 3. 场景 T/Y 交互

- `T`：循环选中附近活动 NPC Agent，并高亮模型。
- `Y`：确认并打开 `NpcDialogueOverlay`。
- 场景外：`Y` 继续作为命令台快捷键。
- 候选条件：
  - Agent 存活且活动
  - 年龄成年
  - 非玩家
  - 非敌对阵营
  - 非战斗/竞技场/决斗/潜行/锦标赛模式

## 4. 遭遇面谈

- 仅当 `PlayerEncounter.Current != null`
- `EncounterState` 为 `Begin` 或 `Wait`
- 不在战斗、攻城、劫掠、敌对遭遇上下文中
- 目标为成年英雄或领队角色
- 原版“谈话”保持并行

## 5. 覆盖层生命周期

- `AwakeMessengerOverlay` 打开时，事件对话队列不得再打开 `NpcDialogueOverlay`。
- `NpcDialogueOverlay` 打开时，通讯录不得覆盖它。
- 场景退出、对话关闭、菜单切换时清理高亮和活动服务。

## 6. 验收

1. 场景内按 T 可循环高亮人物，按 Y 进入 AI 对话。
2. 遭遇菜单出现“面谈（醒世）”，原版“谈话”仍可用。
3. 通讯录打开后，事件不会强开旧覆盖层。
4. 敌对/战斗场景不出现面谈和场景选择。
5. 构建、SdkSmoke、本地化全绿。

## 7. 不做的

- 不合并 `NpcDialogueOverlay` 与 `AwakeMessengerOverlay`
- 不替换原版对话 UI
- 不新增远程写信代码
- 不新增群聊
