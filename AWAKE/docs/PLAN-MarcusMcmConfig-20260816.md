# AWAKE · Marcus MCM 便捷配置

> 日期：2026-08-16

## 目标

在 AWAKE MCM 的 `0. AI 链路` 组内提供便捷配置入口。

## 实现

- 状态读取：Host、声明路由、健康组件。
- 一键同步路由：调用 `FrameworkHost.EnsureDeclaredRoutesAsync`。
- 打开 AI 设置台：调用 `FrameworkConsole.OpenAiSetup`。
- 打开诊断台：调用 `FrameworkConsole.OpenDiagnostics`。
- 重新检测：刷新状态文本。

## 边界

- 不写 `platform.db`。
- 不直接调用 Companion CLI。
- Provider/Key 仍由 Marcus 管理。

## 验证

- SdkSmoke：离线状态文本、按钮动作存在。
- 游戏内：MCM 一键同步/打开设置/重新检测可用。
