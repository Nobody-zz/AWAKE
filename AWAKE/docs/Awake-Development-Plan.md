# AWAKE 开发方案

> 日期：2026-08-16
> 状态：与当前运行时对齐。旧版 Slaanesh 时代方案归档为 `docs/archive/Awake-Development-Plan-20260812.md`。

## 1. 定位

- 运行时：`AWAKE: Awakened World AI / 醒世`，通用 AI 世界运行时，不绑定特定世界观。
- 内容包：`SlaneshsEmbraceContent / 斯拉涅斯之拥`，承载世界书、事件、信件、NPC 主动基础；女神人格与情色机制是独立内容包支线。
- 运行时只依赖 MarcusAIFramework 与 Bannerlord 前置，不依赖 AF / 爱与恨。

## 2. 当前基线

- ModId `AWAKE`，DLL `Awake.dll`，namespace `Awake`，路由/存储/日志均使用 `awake.*`。
- 运行时源码与本地化文件名已统一为 `Awake*` / `awake_*`。
- NPC 深谈入口为 AWAKE 命令台：MCM 可配置快捷键（默认 `U`），深谈复用 `NpcDialogueLauncher`。
- 运行时已有 NPC 对话、跨会话记忆、本地知识检索、世界状态存储、权限与命令桥骨架。
- 事件引擎骨架已加入运行时：规则注册、每小时评估、冷却/每日上限持久化、事件弹窗、对话动作队列；具体事件内容留待内容包接入。
- 事件类型边界见 `docs/Awake-Event-Type-Inventory-20260816.md`，七条分类线已落成代码枚举并接入校验。
- 原生命令底座已加入运行时：`awake.relationship.delta.v1` 可校验、入队、持久化，并会把信任/爱意/敌意状态写回 NPC 对话提示词。
- 内容包公开 API 仍是草案；`SlaneshsEmbraceContent` 当前为骨架工程，`frozen/` 保留旧内容实现。

## 3. 版本线

| 版本 | 范围 | 当前状态 |
| --- | --- | --- |
| `0.1.x` | 运行时核心：NPC 深谈、公开 API、存储管道、知识、配置、双路径验收 | 进行中 |
| `0.2.x` | 内容包基础：世界书、事件、信件、NPC 主动基础 | 未开始 |
| `0.3.x` | 世界模拟：周报/季报/公告/政令/世界事件 | 未开始 |
| `0.4.x` | 关系与记忆深度 | 未开始 |
| `0.5.x` | 内容系统与工具 | 未开始 |
| `0.6.x` | 体验完善 | 未开始 |
| `0.7.x` | 生态与跨模组 | 未开始 |
| `0.8.x` | 性能与可观测 | 未开始 |
| `0.9.x` | 稳定与发布 | 未开始 |

完整路线图见 `docs/Awake-Roadmap-0.1-0.9-20260815.md`。

## 4. 下一步

1. 锁定 v0.1.x 公开 API 计划，走 grill-me 后再实现。
2. 验证 `awake.npc.memories` / `awake.event_meta` 的真实 Companion 存储管道。
3. 完成无内容包双路径验收后，再接入 v0.2.x 内容包基础。

## 5. 不做的事

- 不重做 MarcusAIFramework 底层能力。
- 不在运行时硬编码女神、发情、身体、俘虏等内容语义。
- 不在内容包接入前推进内容包内部玩法。
