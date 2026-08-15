# AWAKE 对话功能待 grill-me 整理稿

> 日期：2026-08-16
> 状态：整理稿，不是已 APPROVED 的 PLAN
> 规则：本批未走完 grill-me 前，不继续扩展对话功能代码

## 1. 目的

当前有一批对话功能已经写了代码，但没有按项目规则走 grill-me。先把这些功能整理成可审查的批次，再逐批盘问和对抗审查。

## 2. 当前功能盘点

| 功能 | 现状 | 是否有独立 PLAN | 是否已 APPROVED |
| --- | --- | --- | --- |
| 通用 NPC 对话基础 | 代码已有 | `PLAN-NpcAiDialogue-20260814.md` | 已 APPROVED |
| NPC 跨会话记忆 | 代码已有 | `PLAN-NpcMemory-20260815.md` | 已 APPROVED |
| NPC 对话知识注入 | 代码已有 | `PLAN-NpcDialogueKnowledge-20260815.md` | 已 APPROVED |
| 无名 NPC 身份回退 | 代码已有 | `PLAN-Awake-AllNpcDialogue-20260816.md` | 未 APPROVED |
| 通讯录 / Messenger M1 | 代码已有 | `PLAN-Awake-Messenger-20260816.md` | 未 APPROVED |
| 遭遇面谈 | 代码已有 | 无独立 PLAN | 未 APPROVED |
| 场景 T/Y 选择对话 | 代码已有 | 无独立 PLAN | 未 APPROVED |
| 远程写信 / NPC 来信 | 未实现 | 已并入 Messenger 计划 | 未 APPROVED |
| 群聊 | 未实现 | 已并入 Messenger 计划 | 未 APPROVED |

## 3. 需要 grill-me 的批次

### Batch A：对话入口与 UI 边界

范围：

- 通讯录 / Messenger M1
- 场景 T/Y 选择对话
- 遭遇面谈
- 原版对话 UI、`NpcDialogueOverlay`、`AwakeMessengerOverlay` 三者的边界

需要盘问：

- 场景内 AI 对话到底用哪套 UI？
- 通讯录是不是只负责远程/异步通信？
- T/Y 的交互是否要保留“选择模型 + 高亮 + 确认”？
- 遭遇面谈和场景 T/Y 是否会造成入口重复？

### Batch B：无名 NPC 对话

范围：

- `AwakeNpcTarget`
- 无名 NPC 身份回退
- 无名 NPC 记忆策略

需要盘问：

- 无名 NPC 的稳定 ID 是否可靠？
- 同一兵种多个 NPC 是否会串记忆？
- 无名 NPC 是否允许关系/命令效果？

### Batch C：远程通信

范围：

- Messenger M3：写信、NPC 主动来信、未读、历史
- 距离计费与回复延迟
- 地图通知

需要盘问：

- 写信是否必须内嵌通讯录？
- 未读和历史用什么存储？
- NPC 主动来信的频率与触发条件？

## 4. 已经确认的边界

- 原版对话 UI 和原版对话机制永远保留。
- `NpcDialogueOverlay` 保留，用于场景/面谈 AI 对话。
- `AwakeMessengerOverlay` 用于通讯录、写信、来信、历史。
- 两个覆盖层不合并、不互删。
- 遭遇面谈保留。

## 5. 当前不继续做的

- 不继续扩展 Messenger 功能
- 不继续加新对话入口
- 不继续补远程写信
- 不继续做群聊

## 6. 下一步

1. 对 Batch A 走 grill-me
2. 对 Batch B 走 grill-me
3. 对 Batch C 走 grill-me
4. 每一批 `VERDICT: APPROVED` 后才允许继续实现
