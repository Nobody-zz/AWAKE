# AWAKE · Messenger 会话持久化

> 日期：2026-08-16

## 目标

通讯录聊天历史跨面板、跨存档会话可回读。

## 方案

1. 新增 `awake.messenger` namespace。
2. `AwakeMessengerHistory` 缓存按 `TargetId` 分组的聊天行。
3. 玩家发送与 NPC 回复时追加并写入存储。
4. 打开联系人时从存储回读历史并填充 UI。

## 验证

- SdkSmoke：messenger store roundtrip。
- 游戏内：关闭重开通讯录后历史仍在。
