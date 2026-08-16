# 斯拉涅斯之拥 · AF 宿主/桥接架构评估

> 日期：2026-08-15
> 背景：在已有 AF 官方源码与已装 AF v1.3.2 DLL 的基础上，评估“以 AF 为底座做自己的逻辑/框架”是否可行。
> **状态：已否决（2026-08-15）**。用户明确决定斯拉涅斯之拥不与 AF 做任何运行时兼容，不引用 AF DLL，不建桥接层。本文保留仅作决策记录，不实施。

## 结论

**不再推荐实施任何桥接。** 斯拥保持完全独立：不依赖 AF、不引用 AF、不检测 AF、不做“AF 存在时增强”的可选兼容层。AF 源码仍可作离线参考，用于算法、数据结构和 UI 模式的借鉴，但所有借鉴必须重写进 Marcus 架构，且不得在运行时与 AF 产生任何交互。

## AF 对外接口验证（本机 v1.3.2 已装 DLL）

以下接口在本机 `Modules\AnimusForge\bin\Win64_Shipping_Client\versions\1.3\AnimusForge.dll` 中均为公开静态/公开类型，已用反编译确认：

| AF 接口 | 用途 |
| --- | --- |
| `AIConfigHandler.GetLoreContext(inputText, Hero/CharacterObject, ...)` | 直接拿 AF 世界书/规则检索上下文 |
| `KnowledgeLibraryBehavior.LoreRule/LoreWhen/BuildLoreContext(...)` | 更底层的世界书构建与规则模型 |
| `ShoutBehavior.TrySystemNpcShout(Agent, content)` | 让场景内 NPC 直接开口说话 |
| `ShoutBehavior.OnApplicationTickForMainThreadActionsExternal()` | AF 暴露给外部的主线程动作排空入口 |
| `ShoutBehavior.CanSubmitNativeConversationForExternal()` / `IsNativeConversationInputOpenForExternal()` | 判断当前是否处于可用的原生对话/喊话上下文 |
| `MyBehavior.RecordNpcActionForExternal(...)` / `RecordPlayerActionForExternal(...)` | 把斯拥事件写入 AF 的 NPC/玩家履历 |
| `MyBehavior.AppendExternalDialogueHistory(...)` | 向 AF 对话历史追加外部事件 |

限制：AF 没有正式插件注册表或版本契约，`ForExternal` 属于“约定俗成”的公共静态入口，会随 AF 版本漂移。所以桥接层必须带兼容探测（方法存在、签名匹配），缺失时整体禁用对应功能，不能把 AF 依赖写进核心。

## 三条路线对比

| 路线 | 优点 | 缺点 | 适合场景 |
| --- | --- | --- | --- |
| A：完全以 AF 为底座 | 立即获得对话、世界书、记忆、终端、主动聊天；开发最快 | AF 不是稳定框架；Bootstrap 双版本 + 大量 Harmony；仓库无 LICENSE；与“未来用 Marcus 替代 AF”冲突；版本一更新就断 | 只想快速出可玩竖切，不关心长期替换 |
| B：完全独立（当前） | 无 AF 依赖；Marcus 是正式框架；可测、可长期维护 | 世界书/记忆/场景对话都要自己重写，速度慢 | 长期产品线 |
| C：Marcus 核心 + AF 可选桥接（推荐） | 快速借用 AF 的沉浸能力；核心逻辑独立；AF 缺失可降级；未来可整体摘掉桥接 | 桥接层需要维护兼容探测；短期多一份适配工作量 | 既想借力 AF，又保留 Marcus 主线 |

## 桥接层范围（第一版）

1. **世界书桥接**：`SlaaneshWorldbookAfBridge` 调用 `AIConfigHandler.GetLoreContext` 取 AF 世界书，再与斯拥四档世界书合并注入 NPC 对话；AF 缺失时只用斯拥 RAG。
2. **场景喊话桥接**：事件触发时用 `ShoutBehavior.TrySystemNpcShout` 让 NPC 直接开口；调用前用 `CanSubmitNativeConversationForExternal` / `IsNativeConversationInputOpenForExternal` 判断上下文，避免乱喊。
3. **主动聊天动机借鉴**：参考 `CompanionProactiveChatBehavior.cs` 的动机模型（状态、冷却、动机疲劳、队伍快照），但状态仍存斯拥 `WorldStateStore`，不读 AF 内部存储。
4. **记忆事实同步**：斯拥事件结算后调用 `MyBehavior.RecordNpcActionForExternal` / `AppendExternalDialogueHistory` 投喂 AF 履历；AF 缺失时只写斯拥自己的记忆。
5. **主线程纪律**：桥接层所有 AF 调用走 `SlaaneshUiDispatcher`，需要时显式调用 `ShoutBehavior.OnApplicationTickForMainThreadActionsExternal()`，与现有覆盖层生命周期一致。

## 关键约束

- 编译期引用：第一版直接引用本机 `versions/1.3/AnimusForge.dll` 会导致绑定具体版本，所以桥接层单独成 csproj，核心 csproj 不引用 AF。
- 兼容探测：所有 AF 调用先反射/探测方法签名；不满足就禁用该桥接功能并写 `af_bridge_disabled` 日志，不影响核心。
- 状态权威：Marcus 存储仍是唯一权威；AF 只负责展示、沉浸、记忆投喂，不能反向覆盖斯拥存档。
- 授权：仓库无 LICENSE；桥接只调用公开 API，不复制 AF 代码；以后若要复制算法或长代码段，先与作者确认。
- 验收：AF 缺失时斯拥全功能可用；AF 存在时世界书/喊话/记忆同步生效；日志统一 `af_bridge_*` 前缀。

## 落地顺序

1. 新建独立桥接工程 `SlaaneshsEmbrace.AFBridge`（可选依赖 AF，核心不依赖）。
2. P0：世界书桥接 `GetLoreContext` 注入 NPC 对话。
3. P0：事件场景喊话 `TrySystemNpcShout`。
4. P1：记忆同步 `RecordNpcActionForExternal` / `AppendExternalDialogueHistory`。
5. P1：主动聊天状态机（借鉴源码，重写存储）。
6. 每批按 grill-me 走 PLAN + APPROVED。
