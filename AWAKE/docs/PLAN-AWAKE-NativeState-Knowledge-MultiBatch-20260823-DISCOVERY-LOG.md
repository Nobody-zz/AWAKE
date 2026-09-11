# AWAKE 原版状态与 NPC 知识多批次边界发现日志

> 状态：`needs_review`
>
> 本日志记录方案发现阶段的静态证据和子代理意见，不是 `grill-me` 锁定计划的正式 review log；正式锁定后另建同名 `PLAN-...-REVIEW-LOG.md`。
>
> 日期：2026-08-23

## 1. 当前结论

- 当前最高证据等级：`E0`。
- 未修改 AWAKE 源码、`dist`、游戏目录、`PlayerExports` 或冻结运行时候选。
- 未启动 Bannerlord、未调用云端 Provider、未调用本地 Worker。
- 当前无执行租约。
- 方案应先完成 D1–D6 决策签收，再进入 `grill-me-codex`、独立只读计划审查和实现批次。

## 2. 子代理节点

### 2.1 runtime-boundary-audit

- Agent：`Carver` / `01a0301e-602f-7f62-a9c6-afa7837ef82e`
- Role：explorer + architect
- Scope：Bannerlord 原版关系、AWAKE 生命周期、上下文、命令、存储、主动 NPC、写回边界
- Status：`needs_review`
- Write set：none

关键结论：

- AWAKE 当前没有接入原版关系/敌友/家族关系 API。
- 当前关系状态是 `hero.<heroId>.v1` 的 AWAKE 自定义三轴，不是原版镜像。
- `NpcDialogueService` 和 `NpcProactiveService` 目前没有 native social provider。
- `CampaignSessionReady` 的 Storage readiness 与 onboarding/事件队列加载存在时序风险。
- `AwakeEventBehavior.OnHourlyTick` 同时启动多个异步任务，主动 NPC 没有单飞闸门。
- 原版写回必须另立批次，不能和首次只读读取混合。
- 推荐运行时批次：B0 合约、B1 原版快照、B2 纯投影、B3 对话接线、B4 知识权限、B5 主动 NPC、B6 原版写回。

证据重点：

- `src/SubModule.cs:21,45,79,126`
- `src/ProbeExtension.cs:95,122,195,202,242`
- `src/AwakeRuntime.cs:220,439`
- `src/NpcDialogueService.cs:430,586,1262`
- `src/NpcProactiveService.cs:79,204,216,280,306`
- `src/WorldStateStore.cs:149,253,2652`
- `docs/mappings/bannerlord-native/关系-家族-家庭-外交原生映射表-v1.md:24,36,65,242`

### 2.2 knowledge-boundary-audit

- Agent：`Halley` / `01a0301e-60d1-77b3-acaf-2be139853015`
- Role：explorer + architect
- Scope：世界书 v2、身份权限、旧检索回退、玩家传授、事件/周报、NPC 记忆、AI/Token/污染边界
- Status：`needs_review`
- Write set：none

关键结论：

- 世界书 v2 已有唯一权限查询顺序：`blocked → identity → conditions → denies → grants → detail → referral → not_found`。
- `WorldKnowledgeQueryService` 当前以关键词索引为主，不是已接入的语义 RAG。
- 旧 `KnowledgeService` 仍存在运行时回退，旧 `KnowledgeRuntime` 权限回调恒为允许，存在身份越权污染风险。
- 玩家传授的 `BeliefResolution` 目前只有数值设计稿，没有独立运行时结算链。
- 现有 NPC 记忆有幂等、容量和摘要路径，但没有明确 `epistemicStatus/confidence/visibility` 知识字段。
- 事件和周报已有唯一 Recorder/Report 路径，没有逐 NPC AI 学习循环。
- 当前事件可见性、传播目标、actor/location/identity 信息不足，不能凭空广播给所有 NPC。
- Overlay 已有 CAS、导入/导出接口，但正式 Campaign Storage/`SyncData` 接线需要单独确认。
- 推荐知识批次：A 统一入口、B 身份快照、C 玩家传授、D 事件/周报传播、E 记忆持久化、F AI/Token/CPU/污染门。

证据重点：

- `docs/AWAKE-Worldbook-Contract-v1.md:5-46`
- `docs/WORLDBOOK-EVENT-REPORT-CONTRACT-v1.md:7-21,35-40`
- `src/WorldbookRuntime.cs:78-125`
- `src/WorldKnowledgeLoader.cs:160-169`
- `src/WorldKnowledgeQueryService.cs:89-218`
- `src/BannerlordWorldbookIdentityAdapter.cs:7-57`
- `src/WorldbookIdentityCapabilityRules.cs:17-114`
- `src/WorldbookIdentityEvaluator.cs:26-105`
- `src/NpcDialogueService.cs:452-478,903-1108,1045-1057,1142-1187,1262-1321`
- `src/KnowledgeService.cs:155-371`
- `src/KnowledgeRuntime.cs:12-22`
- `src/NpcMemoryService.cs:345-382,536-603`
- `src/WorldStateStore.cs:292-414,2424-2471,2544-2649`
- `src/WorldEventContracts.cs:14-77,141-158`
- `src/WeeklyReportService.cs:13-58`

## 3. 已整合的主计划

两份报告已整合进：

`docs/PLAN-AWAKE-NativeState-Knowledge-MultiBatch-20260823-DRAFT.md`

主计划当前采用 B0–B11 结构：

1. B0 合约、数值和边界冻结；
2. B1 运行时就绪与原版/身份统一只读快照；
3. B2 唯一世界书查询和结构化拒绝结果；
4. B3 原版社会状态纯语义投影；
5. B4 对话只读接线；
6. B5 玩家传授与 NPC 相信结算；
7. B6 事件事实、机械周报和受控知识传播；
8. B7 NPC 知识分层、容量和存档恢复；
9. B8 AI、Token、CPU 和污染门；
10. B9 主动 NPC 受控接线；
11. B10 可选原版关系写回；
12. B11 E4/E5 整合验证。

## 4. 当前推荐决策

| 决策 | 推荐 |
|---|---|
| D1 旧 `KnowledgeService` | NPC 正式运行时不走；仅保留显式、可观测、复用权限门的离线回退 |
| D2 玩家传授存储 | v1 复用 `awake.npc.memories`，使用 typed knowledge entry；容量不足再另立 namespace |
| D3 周报传播 | 周报只展示；NPC 直接读取结构化事件/窗口，按可见性和身份传播，不读取周报文本 |
| D4 Overlay 持久化 | Campaign Storage/WorldStateStore，保留 CAS 与导入/导出，不写入 NPC 记忆 |
| D5 native snapshot 范围 | v1 固定“玩家 → 当前对话 NPC”，DTO 保留 source/target；NPC-NPC 另批次 |
| D6 原版写回 | 只读和 E4/E5 稳定后再独立评估，不纳入第一批 |

## 5. 未决与停点

以下事项不能由静态代码安全替用户做最终产品选择：

- 是否正式关闭旧 `KnowledgeService` 的 NPC 运行时回退；
- 是否接受 v1 复用 `awake.npc.memories` 而不是立即新建 knowledge namespace；
- 周报是否允许在明确可见性规则下生成 NPC 知识条目；
- Overlay 是否以 Campaign Storage 作为唯一运行时权威；
- v1 是否坚持只做玩家到当前 NPC 的 native snapshot；
- 原版关系写回是否进入后续版本目标。

这些决定应在 `grill-me-codex` 中逐项确认，并写入锁定版计划与正式 review log。

## 6. 下一步

1. 向用户逐题确认 D1–D6，推荐答案随问题给出。
2. 用户确认后，从本 DRAFT 生成不带 `DRAFT` 的锁定 `PLAN`。
3. 初始化正式 `PLAN-...-REVIEW-LOG.md`。
4. 进行独立只读 Codex 对抗审查，最多 5 轮。
5. 取得 `VERDICT: APPROVED` 和用户签收后，只为一个批次建立实现租约。

