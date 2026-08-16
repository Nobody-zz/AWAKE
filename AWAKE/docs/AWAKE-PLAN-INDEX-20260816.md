# AWAKE 计划与文档总索引

> 日期：2026-08-16
> 目的：把分散的路线图、改进方向、落地方案、PLAN、审查日志、清单和 AF 学习文档统一成一张索引。
> 规则：新计划必须先登记到本索引；状态变化必须同步；旧文档进 `docs/archive`，不在正文当权威。

## 一、当前执行链（先读这几个）

按顺序阅读，避免“方向/计划/任务”互相打架：

1. `AWAKE-Task-Queue-20260816.md`：每日操作队列，轮询当前任务、阻塞、待修复。
2. `AWAKE-Improvement-Directions-20260816.md`：改进方向，说明“为什么做”。
3. `AWAKE-Landing-Plan-20260816.md`：B0-B9 落地批次，说明“先做什么、后做什么”。
4. `PLAN-SceneVisualSelection-20260816.md`：下一份已 APPROVED、待签收的实施方案。
5. `Awake-Development-Plan.md`：与当前运行时对齐的开发方案。
6. `Awake-Roadmap-0.1-0.9-20260815.md`：版本级路线图草案。

## 二、状态图例

- `approved`：已通过独立审查，等待实现或用户签收。
- `draft`：草案，未锁定。
- `implemented`：代码/文档已落地。
- `pending_game`：代码已落地，等待游戏内验收。
- `reference`：参考/清单/契约，不直接驱动开发。
- `archived`：已归档，不作为当前权威。
- `superseded`：被更新文档取代，仅作历史。

## 三、活跃计划（待执行/待签收/草案）

| 文档 | 状态 | 下一步 |
|---|---|---|
| `PLAN-SceneVisualSelection-20260816.md` | approved | 用户签收后实现双模式对话 |
| `Awake-Roadmap-0.1-0.9-20260815.md` | draft | 按 Landing Plan 分批锁定 |
| `Awake-Worldbook-Interface-Spec-20260816.md` | draft | 转成代码接口与校验 |
| `Awake-API-Contract-20260815.md` | draft | 内容包公开 API 落地 |
| `Gameplay-Enrichment-Plan-20260815.md` | draft | 并入 Landing Plan B3-B7 |
| `PLAN-Awake-AllNpcDialogue-20260816.md` | draft | 拆成 Batch 并 grill-me |
| `PLAN-Awake-Dialogue-BatchA-20260816.md` | draft | 场景选人部分被 SceneVisualSelection 取代，入口路由部分保留 |
| `PLAN-Awake-Dialogue-BatchB-20260816.md` | draft | 无名 NPC 对话待锁定 |
| `PLAN-Awake-Dialogue-BatchC-20260816.md` | draft | 远程写信/来信待锁定 |
| `PLAN-Awake-Dialogue-GrillBatch-20260816.md` | draft | 对话功能整理稿 |
| `PLAN-Awake-Messenger-20260816.md` | draft | Messenger 统一会话待锁定 |
| `PLAN-ContactHubHistory-20260816.md` | approved | Round 7 APPROVED；待用户签收 |
| `PLAN-Interactions-20260816.md` | approved | Round 9 APPROVED；待用户签收 |
| `PLAN-UnifiedDialogueSession-20260816.md` | approved | Round 5 APPROVED；待用户签收，前置 ContactHubHistory 已 APPROVED |
| `FEATURE-BRAINSTORM-20260813.md` | reference | 候选池，不是承诺 |
| `FEATURE-FEASIBILITY-RANKING-20260813.md` | reference | 候选排序，需重新对齐现状 |

## 四、已落地代码（待游戏内验收）

| 文档 | 状态 | 关联代码 |
|---|---|---|
| `PLAN-Awake-AF-Batch1-20260816.md` | pending_game | NPC 主动状态机、60 秒 Esc |
| `PLAN-Awake-AF-Batch2to5-20260816.md` | pending_game | 命令台/收件箱/周报/记忆日结/回复清洗 |
| `PLAN-DevTestTools-20260816.md` | pending_game | 开发者测试工具 |
| `PLAN-EventInboxUI-20260816.md` | pending_game | 事件收件箱 Gauntlet |
| `PLAN-WeeklyReportBrowser-20260816.md` | pending_game | 周报浏览器 |
| `PLAN-WorldEventPersistence-20260816.md` | pending_game | 世界事件持久化与周报生成 |
| `PLAN-MarcusMcmConfig-20260816.md` | pending_game | MCM 便捷配置 |
| `PLAN-MessengerPersistence-20260816.md` | pending_game | Messenger 会话持久化 |
| `AWAKE-HalfDone-Inventory-20260816.md` | reference | 半成品与缺口清单 |
| `Awake-StorageAndMemory-Verification-20260816.md` | reference | 存储/记忆游戏内验收清单 |

## 五、参考/契约/清单

| 文档 | 用途 |
|---|---|
| `AWAKE-AI-Architecture-Inventory-20260816.md` | AI 架构现状 |
| `Awake-Event-Type-Inventory-20260816.md` | 事件类型枚举与校验 |
| `AWAKE-MCM-Reference-20260816.md` | MCM 菜单参考 |
| `AWAKE-Worldbook-Migration-Spec-20260816.md` | 世界书迁移规范 |
| `AwakeSplit-ContentClassification-20260815.md` | 内容/代码去向表 |
| `AwakeSplit-Phase0-Inventory-20260815.md` | 拆分边界 |
| `AwakeSplit-SplitScheme-20260815.md` | 拆分落地版 |
| `Marcus-API-Gap-List.md` | 马库斯能力缺口 |
| `Marcus-OneClick-Feasibility-20260816.md` | 一键配置可行性 |
| `AWAKE-Dialogue-Feasibility-20260816.md` | 对话可行性核验 |
| `AWAKE-UX-Dev-Improvements-20260816.md` | 体验/上手/测试建议 |
| `PROVIDER_SETUP.md`、`profiles.awake.*.json` | Provider/路由配置参考 |

## 六、AF 学习与 grillme 文档

| 文档 | 用途 |
|---|---|
| `AF-History-Evolution-20260816.md` | AF 历史版本演进分析 |
| `AF-CRITICAL-EVALUATION-20260815.md` | AF 缺点评估 |
| `AF-Source-Reuse-20260815.md` | 可复用点 |
| `AF-Structures-Landing-Plan-20260816.md` | AF 结构落地 |
| `AF-Feedback-Learning-20260816.md` | AF 反馈学习 |
| `AF-UI-Assets-Inventory-20260816.md` | AF UI 资产盘点 |
| `AWAKE-UI-Borrow-Map-20260816.md` | UI 借鉴映射 |
| `GRILLME-AF-BATCHES-20260816.md` | AF 批次复查 |
| `GRILLME-FINAL-REVIEW-20260816.md` | 最终对抗审查 |
| `GRILLME-SELF-CHECK-20260816.md` | 代码自查 |
| `GRILLME-WORLDBOOK-30-ROUNDS-20260816.md` | 世界书 30 轮审查 |

其余 `AF-*` 文档为历史分析、内容清点、迁移选择和借鉴清单，统一归入参考，不再单独作为开发计划。

## 七、已归档（不读作当前权威）

- 位置：`docs/archive/`
- 数量：205 个文件。
- 内容：旧 Slaanesh 时代 PLAN、10 批 grillme 日志、30/50 轮审查、旧开发方案、旧框架使用地图、旧拆分与修复记录。
- 处理：只作为历史证据；当前任何决策以“当前执行链”和本索引登记的活跃文档为准。

## 八、需要收敛的重复/冲突

| 冲突 | 结论 |
|---|---|
| `PLAN-Awake-Dialogue-BatchA` 的“不做地面预览” | 被 `PLAN-SceneVisualSelection` 取代；场景选人按新版执行 |
| `UPDATE-PLAN-20260813.md` | 旧 Slaanesh 更新计划，被 `Awake-Development-Plan.md` + `AWAKE-Improvement-Directions` 取代 |
| `Awake-Roadmap` vs `AWAKE-Landing-Plan` | Roadmap 是版本方向，Landing Plan 是执行批次，二者不是同一层 |
| `FEATURE-BRAINSTORM` / `FEASIBILITY` | 是候选池，不是当前承诺；进入实施前需重新 grill-me |
| `AF-STANDALONE-MERGE-PLAN` / `AF-REPLACEMENT-STRATEGY` | 历史策略文档，仅作学习，不执行 |
| `AwakeSplit-*` | 已完成拆分记录，作为边界参考，不重复执行 |

## 九、文档治理规则

1. 新计划/新方案先登记到本索引。
2. 每个计划必须标 `draft / approved / implemented / pending_game / archived`。
3. 被取代的计划标记 `superseded`，正文保留但不再作为执行依据。
4. 旧文档只进 `docs/archive`，不删除 git 历史。
5. 每次批次完成同步更新任务队列、本索引和 `BUILD_VERIFICATION.txt`。
