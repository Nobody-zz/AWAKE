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

## 8. 参考 AF / Alice

- AF `AnimusForgeNativeConversationOverlay`：场景内 AI 对话使用独立覆盖层，正文贴近原版对话。
- AF `NpcDataPacket`：场景候选携带 AgentIndex、身份、名字、角色描述。
- Alice `ChatPanel`：联系人/聊天/输入/建议/历史集中在同一面板。
- Alice `NPCConversationManager`：会话区分 `single / letter / multi`。

不照搬：

- AF 的 T/Y 是“喊话范围预览”，不是对话选择；AWAKE 只借用“T 选中、Y 确认”的操作直觉。
- Alice 的 `/` 附近人物弹窗列表不是最终形态；AWAKE 用场景高亮 + 通讯录替代。

## 9. AWAKE 改进点

1. 共享会话服务，双视图：
   - `ImmersiveView`：场景/面谈 AI 对话覆盖层。
   - `MessengerView`：通讯录/远程消息。
   - UI 不合并，但底层服务、历史、命令桥统一，避免两套状态。
2. 场景选中不用 AF 的复杂范围预览：
   - 按下 `T` 时先选“屏幕中心 + 距离权重”最近的人。
   - 再次按 `T` 循环下一位。
   - `Y` 确认。
   - 只高亮模型 + 显示轻量名字提示，不做大量预览标记。
3. 所有入口记录同一 `source`：
   - `scene` / `encounter` / `messenger`
   - 便于日志诊断和后续统一收口。
4. 键位解析统一：
   - 场景内 `Y` = 确认。
   - 场景外 `Y` = 命令台。
   - 后续 MCM 分键配置。
