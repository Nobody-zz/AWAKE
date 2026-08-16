# AWAKE · 事件收件箱 UI 升级

> 日期：2026-08-16

## 目标

把事件收件箱从文本弹窗升级为 Gauntlet 列表界面。

## 实现

- `WorldEventInboxOverlay`：覆盖层。
- `WorldEventInboxVM` + `WorldEventRowVM`：列表数据。
- `WorldEventInbox.xml`：prefab。
- 命令台“事件收件箱”和开发者测试入口统一打开覆盖层。

## 验证

- 构建 0 警告 / 0 错误。
- 游戏内：事件列表可滚动、Esc 关闭、无重叠。
