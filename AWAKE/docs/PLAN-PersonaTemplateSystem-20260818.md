# Plan: AWAKE Persona Template System
_Revised after external read-only review — implementation split into verified batches_

## Goal

先交付不依赖未验证存档回调的 Persona 核心链路：结构化人格数据、稳定标签注册、确定性 Persona DSL 生成器、世界书加载、旧人格兼容回退和 NPC Prompt 接线。存档时间线、分支水位和原子提交作为后续基础设施批次，未具备可靠生命周期与存储契约前不宣称 `save_committed` 或原子事务语义。

## Batches

### Batch A — Prompt-visible Persona core
1. 定义 `awake.persona.definition.v1`、标签注册表、来源/审核状态和缓存元数据；明确字段上限、未知字段策略、权威字段与缓存字段。
2. 实现纯确定性生成器：稳定核心、表达、行为、触发、边界分层；按固定回退优先级选择模板；展开 bundle；拒绝未注册标签和未 approved 模板。
3. 新增 Persona 世界书目录与清单入口；旧 `personality_background` 保留为背景事实和兼容回退，不自动升级为 approved。
4. 在 NPC Prompt 构建入口加载当前角色 Persona，按核心人格→身份硬事实→关系→行为→经历→场景→边界顺序生成 DSL，并在预算不足时按优先级裁剪。
5. 生成快照仅作为内存缓存，使用来源哈希、运行时上下文、模板版本、生成器版本和 Prompt 版本组成指纹；指纹变化即重建。

### Batch B — Safe legacy migration and overrides
1. 旧人格长文本只生成 `draft` 候选，不能直接进入正式 Persona。
2. 支持 approved 的玩家覆盖、追加/覆盖/禁用叙事经历；不修改游戏机械事实，保留原始记录和来源。
3. 增加冲突、回退、迁移、预算、未注册标签和 Prompt 接线诊断。

### Batch C — Save/timeline foundation (deferred until lifecycle proof)
1. 先核验 Bannerlord v1.3.15 保存生命周期和 Marcus Storage 是否支持 durable marker、恢复扫描和分支查询。
2. 若能力成立，再定义并持久化 `campaignId/saveId/timelineId/branchId/parentBranchId/forkSequence`，并把 branch 纳入 transcript、memory、persona、ledger、commit-group key。
3. 用每个投影独立 watermark 定义 `acceptedSequence`，不比较跨数据类型的局部序号。
4. 引入 durable commit-group journal：`prepared/committed/aborted`；明确 required/optional participant、幂等键和恢复策略。
5. 在没有可靠 post-save callback 时只实现 pre-save anchor + durable `unsaved_recovery`，不伪造 save-completed 语义。

## Fixed contracts for Batch A/B

- Persona Definition 的权威来源是 approved 内容；GeneratedSnapshot 是可重建缓存，不是事实来源。
- 稳定主键是 `CharacterId`，不使用显示名；文化默认稳定，不实现文化转变机制。
- 标签 ID 使用稳定英文，小写点分隔；中文仅用于作者显示和解释；bundle 运行时必须展开。
- 回退顺序：玩家 approved 覆盖→角色专属 approved→当前内容包→其他内容包→身份→职业→原生性格→最小通用模板。
- 同层冲突显式记录，不按加载顺序静默覆盖；不同内容包的专属人格不自动深度合并。
- 野史/谣言/未确认信息不进入 Persona 硬事实区段。
- Prompt 注入失败时回退旧人格文本，再回退最小通用人格；不阻断基础对话。
- 运行时预算使用现有 UTF-8 字节预算；tokenizer 精确预算留待后续测量，不在本批次虚构阈值。

## Validation

- Batch A/B 必须通过模型解析、标签注册、确定性生成、回退链、冲突拒绝、预算裁剪、旧格式兼容和 Prompt smoke。
- 运行现有双版本构建、SdkSmoke、世界书结构/关键词审计、资产边界和发布检查。
- 构建成功不等同游戏内验收；不启动游戏，不覆盖正在运行的游戏目录。
- Batch C 在保存回调和 Storage 能力有证据前保持 deferred，并单独记录风险。

## Out of scope

文化转变时长、野史系统、游戏内人格编辑 UI、人格模式切换、AI 自动批准人格、存档后回调假设和未验证的跨投影原子事务。