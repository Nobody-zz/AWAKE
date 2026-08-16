# AWAKE · 世界事件持久化与周报生成

> 日期：2026-08-16

## 目标

- `WorldEventLedger` 从内存队列升级为可持久化事件流。
- 每 7 游戏日自动生成周报并写入事件记录。

## 方案

1. 新增 `awake.world.events` namespace 和 `WorldStateKind.WorldEvents`。
2. `WorldEventLedger.Record` 同时写内存与持久化 outbox。
3. 命令台收件箱/周报打开前从存储回读历史。
4. `AwakeEventBehavior` 按日滚动生成周报。

## 验证

- SdkSmoke：world events roundtrip、滚动、周报触发边界。
- 游戏内：重启后历史事件仍在收件箱。
