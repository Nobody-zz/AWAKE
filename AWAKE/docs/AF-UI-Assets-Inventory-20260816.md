# AF UI 资产与交互审计

> 日期：2026-08-16
> 原则：只记录布局/交互思路，不复制 AF prefab 与代码。

## 1. 资产清单

| AF 资产/交互 | AWAKE 对应 | 处理 |
| --- | --- | --- |
| 命令台热键根菜单 | `AwakeTerminalBehavior` | 已复用交互，菜单改为通讯录/收件箱/周报/开发者检查 |
| 场景选人范围 | `AwakeTerminalBehavior` + `SceneDialogueSelection` | 已复用 T/Y/范围交互，距离改三维 |
| 对话覆盖层 | `NpcDialogueOverlay` + `NpcDialogueVM` | 已自建，增加 60 秒 Esc 解锁 |
| 事件收件箱弹窗 | 命令台“事件收件箱”文本弹窗 | 先用轻量文本，后续再升级 prefab |
| 世界周报 | 命令台“世界周报”文本弹窗 | 复用 `NarrativeReportBuilder` |

## 2. 交互规则

- 场景内：T 扩大范围、Y 切换、松开 T 对话。
- 场景外：U 打开命令台。
- 覆盖层：Esc 空闲关闭、等待超 60 秒取消关闭。
- 所有文本弹窗使用游戏原生 Inquiry，不新增自定义 prefab。

## 3. 后续

- 收件箱和周报升级为 Gauntlet 列表 UI 时，自建 VM/prefab，不复制 AF XML。
