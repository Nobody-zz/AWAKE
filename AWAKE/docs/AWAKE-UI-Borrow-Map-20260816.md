# AWAKE · 对应功能 UI 借鉴映射

> 日期：2026-08-16
> 原则：只参考布局/交互，不复制 AF/Alice 的 XML、VM 和命名。

| AWAKE 功能 | AF/Alice 参考 UI | AWAKE 现状 | 该借鉴什么 |
| --- | --- | --- | --- |
| NPC 主动聊天 | AF 主动接触弹窗 + `DisplayMessage` | Inquiry 弹窗 + `AwakeFeedback` | 接受/拒绝按钮、被拦截时保留通知、绿色成功/黄色提醒 |
| Messenger | AF 信使来信弹窗 / Alice 聊天面板 | `AwakeMessenger.xml` | 未读计数、联系人列表 + 聊天区、输入框焦点、加载状态 |
| 世界事件收件箱 | `AnimusForgeWorldEventInboxPopup.xml` | 命令台文本弹窗 | 事件列表、未读标记、大地图通知入口 |
| 世界周报 | `TerminalWeeklyReportBrowserPopup.xml` / `DevWeeklyReportPopup.xml` | 命令台文本弹窗 | 周报分区、生成状态、可回看入口 |
| 命令台 | AF Terminal 根菜单 | `MultiSelectionInquiryData` | 分组菜单、热键提示、拦截原因反馈 |
| 对话覆盖层 | `AnimusForgeNativeConversationOverlay.xml` | `NpcDialogue.xml` | 等待动画、流式状态、Esc 解锁提示 |
| MCM | AF `DuelSettings` 数字分组 | `AwakeConfig` 五组 + 按钮 | 数字前缀、操作按钮、预设、开发者项后置 |
| 游戏内反馈 | AF `InformationManager.DisplayMessage` 颜色体系 | `AwakeFeedback` | 绿/黄/红语义、失败原因即时提示 |

## 落地顺序建议

1. 世界事件收件箱 UI：把文本弹窗升级为 Gauntlet 列表。
2. Messenger 未读计数与来信提示。
3. 周报浏览器。
4. 对话覆盖层等待动画。

## 边界

- 不复制 AF/Alice prefab 与 ViewModel。
- UI 全部使用 AWAKE 命名和现有 Gauntlet 模式。
