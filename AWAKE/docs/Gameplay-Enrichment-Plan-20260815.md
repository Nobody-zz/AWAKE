# 玩法丰富/落地方案（grill-me 输出，Round 1 修订版）

> 状态：ACT 2 Round 1 REVISE 后已修订；对抗审查记录见 `PLAN-GameplayEnrichment-REVIEW-LOG-20260815.md`。

## 目标

在已建成的 AI 对话基础框架上，分五批把“事件引擎 + 对话动作 + 受控命令 + 世界模拟 + 内容入库”做成真实可玩的闭环。每批遵循：grill-me 锁 PLAN → 独立只读审查 APPROVED → 实现 → SdkSmoke/审计 + 游戏日志验证 → 文档/哈希同步。

## P0a：事件引擎 + 对话动作（第一批，0.2.x 内）

不发明通用 JSON 解释器，扩展现有 `SlaaneshEventRule`：

- 字段：规则级 `Weight`、`CooldownHours`（沿用现有引擎单位）、`MaxPerDay`；选项级 `DialogueAction { Choice, TargetId, OpeningHint }`（可选，缺省沿用现有行为），保证 A 选项开对话、B 选项不开。
- `open_npc_dialogue` 动作：选项结算后按稳定 ID 解析目标 Hero → 成年/存活/附近重校验 → `NpcDialogueContext.Record(heroId, openingHint)` → 用 `NpcDialogueLauncher.TryOpenDialogue` 打开；打开失败记 ledger，不伪造成功。
- 弹窗安全：动作不直接在 `ShowInquiry` 回调里开覆盖层；先入队到战役/应用 tick 的待办队列，下一安全点消费；失败有回退与 ledger 语义。
- 冷却/每日上限：`CooldownHours` 与日计数持久化到 `WorldStateStore` 事件元数据 namespace，读档后继续生效，单位全程统一为小时。
- 周报：事件与主动邀约已有 ledger；NPC 对话命令结算的 ledger 记录在 P0b 显式补齐（含 body/estrus 结果），不当作“现状已具备”。

## P0b：对话命令扩容（第二批，ContentPolicy 前置）

先做 `ContentPolicy`（全局档位 + 内容开关），再扩 `AllowedCommandIds` 到 `body.develop.v1` / `estrus.tick.v1`：

> 运行时先落地的原生命令 `awake.relationship.delta.v1` 不属于内容语义，已提前完成；body/estrus 仍按本批 ContentPolicy 前置执行。

- 所有白名单命令强制 `arguments["heroId"] = _heroId`，SdkSmoke 断言不能改他人。
- body 命令的 `gender` 由实际 Hero/身体状态强制写入，不接受 AI 任意填；`action` 在 adapter 白名单为 `develop/decay`。
- `NpcPromptTemplate` 指令、输出 schema、校验器同步按命令给出允许 ID 与必填字段。
- 每个白名单命令独立映射记忆事实（relationship 记关系变化、body 记身体开发、estrus 记发情变化）并 `WorldEventLedger.Record`，进周报。
- 身体/发情数值限幅与男女分区沿用现有 adapter。

### ContentPolicy 契约（P0b 前置）

- 枚举：`ContentTier { Pure, Standard, Intense }`；配置字段 `SlaaneshConfig.ContentTier` 默认 `Standard`；MCM 下拉接入。
- 现有每条硬编码 `SlaaneshEventRule` 补 `ContentTier` 字段（默认 `Standard`）；ContentPolicy 启用时未打标事件校验失败，不静默全过/全丢。
- RAG 四档映射：`Pure → clean`；`Standard → clean + dark`；`Intense → clean + dark + extreme`（bloody 由内容开关决定是否叠加）；P2 建四档语料时按此过滤。
- 过滤点：事件进池按 `ContentTier` 标签过滤；知识检索按档位映射过滤；prompt 允许范围按档位裁剪；每批 SdkSmoke 覆盖三档切换。

## P1：世界模拟

- 周报/季报持久化；世界公告与政令效果走受控命令。
- P1 命令契约独立成 PLAN：先锁 `world.decree.v1` / `world.report.v1` 的输入/输出 schema、权限、每日上限与 manifest，审查 APPROVED 后才实现；本方案只排期不伪锁。
- 世界事件（战争、围城、联姻、流言）按王国/文化/关系过滤进入事件池。
- ContentPolicy 统一读：事件进池、知识检索、提示词范围。

## P2：内容入库

- 定义目标 JSON schema 与 388 事件迁移转换器；先做一次 388 计数审计再迁移。
- 事件 manifest + ContentHash 审计/刷新脚本；`ModuleData/HoukaiEvents` 进入 dist/game/test 同步。
- 四版世界书转带档位元数据的 RAG 语料（集合/指纹按档位分离），再接入检索过滤。

## P3：权力关系玩法（独立批次）

先定义状态模型与命令契约（商队/佣兵/匪徒/俘虏权力关系），审查通过后再排期；不与 P2 内容混排。

## 验证矩阵（每批必含）

- P0a：规则校验、选项级 `DialogueAction`、daily cap、target 缺失、冷却/日计数读档一致、打开失败 ledger。
- P2：corrupt JSON、manifest 哈希不匹配、388 计数审计、档位映射。
- 对话：命令 target 强锁、打开失败 ledger、弹窗队列安全、记忆/知识联动。
- ContentPolicy：档位切换后事件池、检索、提示词过滤一致。
- 游戏内：城镇深谈、事件触发对话、命令结算、记忆读回、读档一致。

## 风险

- 事件 JSON 改动必须重算 ContentHash 并过 manifest，否则发布旧哈希。
- 命令扩容扩大越权面：target 强锁 + 限幅 + SdkSmoke 常驻断言。
- P2 内容量大：分批勾选，“全量搬完”不作为 P0 完成条件。
