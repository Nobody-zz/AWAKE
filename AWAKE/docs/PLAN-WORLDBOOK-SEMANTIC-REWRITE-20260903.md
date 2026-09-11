# Worldbook Semantic Rewrite Pilot Plan

**Goal:** 将下载版卡拉迪亚编年史作为唯一权威来源，先为 30 个试点文件建立 claim 级语义工作表，再对 5 个代表性文件完成“理解 → 提炼 → 重写 → 证据绑定”的语义候选；只有 source registry、content tier 和必要 ID 边界确认后，才允许投影为 Authoring v1 `needs_review` 文档。

**Current date:** 2026-09-03

## Confirmed direction

- 权威来源：`C:\Users\26811\Downloads\卡拉迪亚编年史\卡拉迪亚编年史`
- 不使用四档版作为本批次权威来源。
- 旧 `Keywords`、`RagShortTexts`、`Variants`、`When`、`TextMappings`、`SemanticPrototypes` 只作为证据和分析输入，不能直接复制成目标正文。
- 上一版直接搬运候选已标记为 `superseded`，不得复用其正文。
- 所有新候选保持 `needs_review`；不自动 canon、approved、compiled、published。
- 不修改下载源、旧世界书、Studio 源码、AWAKE 源码、dist、游戏目录或发布包。

## Semantic migration pipeline

1. **Source reading:** 逐文件读取 30 个试点源文件，建立 source-unit 证据索引；保留原路径、文件 hash、旧 ID、字段、变体和条件定位。
2. **Atomic extraction:** 将正文提炼为事实、解释、传闻、关系、状态和未决信息；识别同一事实的不同视角，不把视角当作新事实。
3. **Topic clustering:** 允许一个旧文件拆成多个目标 assertion，也允许多个旧文件合并到同一实体/主题文档；记录每个旧段落去向和 loss。
4. **Human-readable rewrite:** 以来源证据为边界重新撰写 summary/detail 文本；不得把旧变体全文作为目标正文，来源原文只保留在 evidence quote。目标 claim 必须记录新增、删减、合并、拆分和改写理由。
5. **Access mapping:** 旧 `When` 默认作为视角/受众证据，不自动成为 grants/denies；只有来源和 registry 能证明的 profile/entity/condition 才能进入权限结构，无法证明的条件保留为 unresolved。
6. **Authoring projection:** 语义候选先保持独立的 authoring-neutral 草稿；source registry、content tier、universe/era 和 ID 边界确认后，才投影为 Authoring v1 候选。所有文档使用 `status=needs_review`，未确认时禁止 compile/export。

## Claim-level worksheet contract

每个 source unit 必须生成可机器校验的语义工作表，至少包含：

- `source_unit_id`、`input_sha256`、`relative_path`；
- `claim_id`；
- `epistemic_kind`：`source_fact`、`interpretation`、`rumor`、`relationship`、`state`、`unresolved`；
- `subject`、`predicate`、`object`；
- `perspective` 或 `speaker_culture`；
- `time_scope`，包括 `valid_from`、`valid_to` 或 `unknown`；
- `confidence`；
- `conflict_group_id`；
- `canonicalization_state`；
- `review_state`；
- `source_locator`、`normalized_quote`、`quote_hash`；
- `legacy_origins`，逐条记录旧 Variant、RAG、Keyword、When、TextMapping 的去向；
- `rewrite_rationale`、`loss` 和 `unresolved`。

### Legacy origin and claim source binding minimum shape

`legacy_origins` 不是自由文本。每个旧输入原子项必须有一个对象：

```text
{
  "origin_kind": "variant|rag|keyword|when|text_mapping",
  "origin_id_or_locator": "relative/path.json#/variants/0/content",
  "destination": {
    "kind": "claim|assertion|expression|permission_evidence|reference_only|unresolved",
    "ids": ["claim_id or target object id"]
  },
  "disposition": "preserve|merge|split|rephrase|drop|unresolved",
  "rationale": "why this disposition was chosen",
  "loss": ["required when disposition is drop or unresolved"]
}
```

每个 claim 的来源绑定必须是：

```text
{
  "claim_id": "authoring_provisional.claim.<hash>",
  "source_id": "source.*",
  "source_version": "immutable version",
  "source_content_hash": "sha256",
  "locator_root": "registered relative root",
  "relative_locator": "relative locator under locator_root",
  "normalized_quote": "normalized source quote",
  "quote_hash": "sha256(normalized_quote)"
}
```

机器不变量：

- 每个旧 Variant、RAG、Keyword、When、TextMapping 原子项必须有且仅有一个 `legacy_origin`；
- 允许一对多、多对一，但必须列出所有目标 ID；
- `drop` 必须有非空 `loss`；
- `unresolved` 不得进入 `source_fact`、active permission 或 current state；
- 每个目标 assertion/expression 必须回溯到一个或多个具体 claim；
- claim source binding 不得只指向宽泛 source unit；
- origin destination、claim、assertion、expression 的 ID 引用必须闭合；
- 任何未绑定或重复绑定的旧原子项阻止 validation。

语义边界固定如下：

- `source_fact` 表示来源明确声称的事实，不等于已经批准的世界正典事实；
- `interpretation` 表示来源中的判断、因果解释、政治评价或文化立场；
- `rumor` 表示来源明确以传闻、据说、民间说法或敌对叙事呈现的内容；
- `relationship` 必须具备 subject/predicate/object，不能只写“关系密切”；
- `state` 必须具备时间或状态边界，当前状态不能从历史段落自动推导；
- 文化、角色和第一人称表达默认保留视角，不自动升级为无视角事实；
- 冲突叙事并存时建立同一 `conflict_group_id`，不通过润色消除差异。

## Anti-mechanical-rewrite acceptance

“目标正文不等于旧 Variant 全文”不再是充分验收。每个重写候选还必须满足：

1. 每条目标 assertion 都引用一个或多个 claim，而不是只引用整段旧 Variant；
2. 每条 claim 记录原始 locator、quote、quote hash、类型、视角、时间、置信度和 loss；
3. 旧 Variant、RAG、Keyword 和目标正文执行规范化句段及 n-gram 比较；
4. 专名、地名和必要术语可列为显式例外，其余高重叠必须有人工 rationale；
5. 目标正文中的新增语义若没有 source claim，必须标记 `unresolved`，不得作为 fact；
6. 目标正文中的删减必须在 loss 中说明为何不进入目标知识；
7. 人工复核项必须明确询问“这是理解后的重写，还是原文的表层改写”。

## Source and ID boundaries

- Source registry 使用 `source_id/source_version/source_content_hash/locator_root`；正典 locator 必须是相对 `locator_root` 的稳定 locator。
- 绝对路径只保留在审计元数据，不写入正典 source locator。
- `normalized_quote` 与 `quote_hash` 必须可独立重算；locator 越界、版本/hash 失配和 unknown license/use 状态阻断。
- Authoring provisional ID、Runtime ID、entity ID、profile ID、source ID 是不同命名空间；provisional ID 不进入 runtime、存档、registry 或 redirect。
- authoring-neutral 草稿使用独立 `authoring_provisional.*` 命名空间；该前缀不得匹配正式 `doc.*`、`assertion.*`、`expr.*`、`entity.*`、`profile.*`、`source.*`、`redirect.*`，也不得被任何 Runtime、存档、registry、ledger 或 redirect validator 接受。
- `provisional_id -> final_id` 只能通过人工确认映射表完成；映射表必须记录 revision、source claim、reviewer 和接受/拒绝理由。
- 显示名、文件名、关键词、数组下标不得直接生成 entity/profile ID。
- 合并、拆分、改名必须在人工确认后建立 ledger、redirect 或 lifecycle 记录。

## Content tier and permission gate

- 语义工作表允许使用 `content_tier=pending`，但该值不能进入 Authoring v1 schema。
- source registry 未明确 `content_tier`、`license_status`、`use_status` 时，候选保持 authoring-neutral `needs_review`，不得伪装为 base。
- 未确认 universe/era 时，不能进入 active/current runtime。
- `When` 只作为 perspective/audience/condition evidence；没有 registry-backed profile/entity 映射时不得生成 grants/denies。
- 空 grants/denies 表示 unknown，不表示公开可读。
- CLI 默认 `--content-tier base` 只适用于 tier resolution 已完成且 graph closure 已证明全为 base 的输入；它不能把 worksheet 的 `pending` 或 `unknown` 解析为 base。
- 必须有负向 fixture：`pending/unknown + omitted --content-tier`、`unknown universe/era + omitted --content-tier`、adult fallback/redirect/index/cache 进入 base，全部必须在写入前阻断。
- `RUNTIME-MAPPING-CONTRACT.md` 中的旧 When 映射仅适用于已经人工确认的 Authoring grants；legacy When 本身只能进入 `legacy_origins`、报告或 unresolved，不能自动生成 grant/deny。
- 每个生成的 grant/deny 必须带 `mapping_basis`，且该 basis 指向已确认的 profile/entity/condition registry ID；缺失时保持空权限并记录 unknown。

## Trial coverage

5 个语义重写文件的能力边界固定为：

- `rule_拉科尼斯湖`、`rule_沙拉斯湾`、`rule_黎明山脉`：三个 baseline perspective rewrite；
- `rule_卡恰尔半岛`：多文化、多变体和历史/战略混合拆分；
- `rule_德里亚特`：bound settlement mapping。

这 5 个文件不代表已验证重复、截断、占位、成人层、来源异常或 merge/split。另需建立 blocked-only fixture，覆盖重复变体、截断/占位、source anomaly、merge/split 和 unknown/adult tier closure。

## Snapshot reproducibility

30 文件 snapshot manifest 必须锁定规范化相对路径、UTF-8/LF/no-BOM 后的文件 hash、文件大小、文件数量、固定排序规则、manifest hash、snapshot hash 组合算法、source root、source version 和生成时间。

确定性算法：

1. `normalized_path`：相对 source root，`\` 转 `/`，Unicode NFKC，按 ordinal case-fold 生成比较键；比较键碰撞直接阻断；
2. `file_entry`：`{path, byte_length, content_sha256}`，对象键固定为 `path`、`byte_length`、`content_sha256`；
3. `manifest_identity`：`{normalization_version:"utf8-lf-no-bom-v1", source_root_id, source_version, files:[file_entry...]}`，按规范 JSON UTF-8、无额外空白、LF 结尾序列化；
4. `manifest_hash = SHA256(manifest_identity_bytes)`；
5. `snapshot_hash = SHA256("awake.worldbook.source-snapshot.v1\n" + manifest_hash + "\n" + source_root_id + "\n" + source_version)`，分隔符为 ASCII LF，编码为 UTF-8；
6. `generated_at` 只记录审计时间，不进入 `manifest_identity` 或 snapshot hash；
7. 非 UTF-8、空路径、越界路径、重复规范化路径和无法读取文件均阻断。

确定性回归：

- 同一 source snapshot 连续生成两次，manifest_hash 和 snapshot_hash 必须一致；
- 只修改一个文件，只允许该文件 hash、manifest_hash 和 snapshot_hash 改变；
- 只修改 generated_at，不得改变 identity hash。

## Positive semantic preservation acceptance

反复制检查不是充分条件。每个重写候选必须有句子/段落到 claim 的覆盖表：

```text
target_span -> claim_ids[] -> source_origin_ids[] -> operation
```

`operation` 只能是 `preserve|merge|split|rephrase|drop|unresolved`。验证每个目标 claim：

- `epistemic_kind` 不得无证据升级；
- perspective/speaker 不得被抹平；
- time scope 不得被扩展；
- relationship/state 不得被改写成无边界事实；
- rumor/interpretation 不得变成 `source_fact`；
- 极性、主体、客体和限定条件不得无记录地改变；
- 所有新增、删减、合并、拆分都必须有 rationale/loss；
- 人工审查结果必须记录 `understood_rewrite`、reviewer、review_state 和 rejection_reason。

验证集必须同时包含：

- 一个低 n-gram 重叠但语义错误的反例；
- 一个高 n-gram 重叠但因专名/必要术语而合理的正例；
- 一个视角被抹平的反例；
- 一个历史状态被错误升级为当前状态的反例。

## First semantic rewrite set

- `knowledge/rules/rule_拉科尼斯湖__拉科尼斯湖.json`
- `knowledge/rules/rule_沙拉斯湾__沙拉斯湾.json`
- `knowledge/rules/rule_黎明山脉__黎明山脉.json`
- `knowledge/rules/rule_卡恰尔半岛__卡恰尔半岛.json`
- `knowledge/rules/rule_德里亚特__德里亚特.json`

暂不作为第一批重写输入：

- `rule_攻城塔`：疑似截断；
- `rule_吕卡隆`：占位符和动态 TextMappings；
- `rule_塞堤斯河`、`rule_贝恩兰岛`、`rule_车尔特格山`：重复变体；
- `rule_巴旦尼亚水之女神`：重复旧 ID；
- `rule_潘德拉克战役`：战争/历史主题边界未先确定。

## Planned artifacts

- `docs/worldbook-migration/SEMANTIC-WORKSHEETS-DOWNLOAD-20260903.json`
- `docs/worldbook-migration/KNOWLEDGE-CLUSTER-PLAN-DOWNLOAD-20260903.json`
- `docs/worldbook-migration/REWRITE-CANDIDATE-5-DOWNLOAD-20260903.json`
- `docs/worldbook-migration/BLOCKED-ONLY-FIXTURES-DOWNLOAD-20260903.json`
- `docs/worldbook-migration/SEMANTIC-REWRITE-VALIDATION-DOWNLOAD-20260903.json`
- `docs/worldbook-migration/PLAN-WORLDBOOK-SEMANTIC-REWRITE-20260903-REVIEW-LOG.md`

## Acceptance criteria

- 30 个 source unit 均有 claim 级语义工作表或明确的 `needs_review` 原因。
- worksheet、cluster plan、rewrite candidate 和 validation report 的 source unit 集合完全相等。
- 5 个重写候选的目标正文不等于任一旧 Variant 的表层改写；全文、句段和 n-gram 重叠均有例外或 rationale。
- 每条目标 assertion 都引用 claim 集，并具备 source registry 允许的相对 locator、normalized quote 和 quote hash；每个目标句/段都有 claim coverage。
- 每条事实、解释、传闻、关系和状态分类都保留视角、时间、置信度、冲突组和人工复核状态。
- 旧变体、关键词、RAG、条件和 TextMappings 都有逐项 `legacy_origin` 去向，不能静默丢失；每个 origin 的 destination、disposition、rationale 和 loss 可机器校验。
- 不从职业、文化、技能或旧角色条件自动推导权限；无 registry-backed 映射时保持 unresolved。
- 不自动把成人或未知层级内容放入 base；未确认 tier 时不投影为 Authoring v1。
- provisional ID 不进入正式 namespace、runtime、存档、registry、ledger 或 redirect。
- `pending/unknown + omitted --content-tier`、unknown universe/era 和 adult closure 均有负向阻断证据。
- blocked-only fixture 能证明重复、截断、占位、来源异常、merge/split 和 unknown/adult closure 会阻断。
- 源文件 SHA-256 前后不变；候选输出写入独立目录。

## Verification

- source snapshot hash and per-file SHA-256 recheck；
- semantic worksheet JSON parse；
- 5-file rewrite candidate JSON parse；
- Authoring v1 schema validation；
- source registry and permission gate validation；
- duplicate/full-text comparison proving no target body equals a legacy Variant；
- no source/workspace/game-directory mutation check。

## Explicit non-goals

- 不修复源文件本身；
- 不决定 source registry 的许可状态；
- 不确认最终 content tier；
- 不创建正式 entity/profile/ID ledger；
- 不生成 runtime v2 包；
- 不 compile/export；
- 不启动 Bannerlord；
- 不调用真实 Provider。
