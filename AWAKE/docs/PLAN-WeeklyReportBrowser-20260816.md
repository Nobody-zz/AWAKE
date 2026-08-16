# AWAKE · 周报浏览器 UI

> 日期：2026-08-16

## 目标

周报从文本弹窗升级为独立 Gauntlet 查看器。

## 实现

- `WeeklyReportBrowserOverlay`：覆盖层。
- `WeeklyReportBrowserVM`：标题、状态、正文。
- `WeeklyReportBrowser.xml`：prefab，仅使用 AWAKE 自有/游戏标准资源。
- 命令台“世界周报”和开发者测试入口统一打开查看器。

## 验证

- 构建 0 警告 / 0 错误。
- 游戏内：正文可滚动、Esc 关闭、无重叠。
