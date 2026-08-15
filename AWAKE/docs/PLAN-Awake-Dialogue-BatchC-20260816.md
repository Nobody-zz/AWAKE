# Batch C：远程写信 / NPC 主动来信

> 日期：2026-08-16
> 状态：待用户签收的修订 PLAN

## 1. 目标

远程通信完全内嵌通讯录，不出现独立收件箱/写信面板。

## 2. 存储

- 新增命名空间：`awake.conversations`
- 每个联系人一个会话：
  - `ConversationId`
  - `TargetId`
  - `LastMessageDay`
  - `UnreadCount`
  - `LastMessagePreview`
- 消息：
  - `ConversationId`
  - `SpeakerId`
  - `SpeakerName`
  - `Text`
  - `GameDay`
  - `Kind`：`player / npc / system`

## 3. 写信

- 远方联系人会话内切换“写信模式”
- 费用：基础 100 + 距离 * 10
- 发送后进入等待，回复延迟 1 天
- 发送记录和回复都写入 `awake.conversations`

## 4. NPC 主动来信

- 只从玩家已熟识/有过对话的 Hero 中选择
- 每日主动来信上限由 MCM 控制，默认 1 封
- 来信进入通讯录联系人会话，显示未读
- 地图通知只提示“有来信”，阅读和回复在通讯录内完成

## 5. MCM

- 启用远程写信
- 启用 NPC 主动来信
- 每日来信上限
- 费用倍率

## 6. 验收

1. 远方联系人可直接在通讯录写信。
2. 1 天后收到回复。
3. NPC 会主动来信并显示未读。
4. 存档重开后未读、历史、最近消息仍存在。
5. 不存在独立收件箱/写信弹窗。
