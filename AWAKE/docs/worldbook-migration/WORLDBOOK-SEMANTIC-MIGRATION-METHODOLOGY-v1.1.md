# Worldbook 语义迁移方法论 v1.1

> 状态：`red_team_repaired / contract_validator_passed / semantic_review_required`  
> 适用：旧世界书、规则库、人物背景、多变体知识库到结构化 Worldbook Studio authoring 的迁移。  
> 当前边界：只涉及迁移审阅、整理和候选生成；不涉及 AWAKE 模组本体闭环、游戏同步或游戏内验证。

## 1. v1.1 修订结论

v1.0 的原则方向正确，但不能独立作为执行标准，因为原则没有全部落到机器契约、状态机和硬拒绝规则。v1.1 将以下内容提升为强制门：

- source atom → claim → target span 三向闭环；
- polarity、主体/谓词/客体、视角、时间和认识论类型保持；
- 所有旧字段和异常都有 source origin；
- `When` 默认不产生权限，未结算不得进入 active expression；
- provisional ID 只允许出现在迁移中间产物；
- pending/unknown 不能被 CLI 默认 base 绕过；
- snapshot hash 有固定规范化算法和 golden fixture；
- accepted/rejected 必须有绑定的人工决策记录；
- 批次状态、artifact hash 和 snapshot 证据必须绑定；
- source registry、父/子 snapshot、candidate 的 join 必须一致。

## 2. 权威执行契约

以下文件是 v1.1 的机器契约：

- `semantic-origin.v1.schema.json`
- `semantic-worksheet.v1.schema.json`
- `semantic-rewrite-candidate.v1.schema.json`
- `semantic-rewrite-review.v1.schema.json`
- `semantic-batch-state.v1.schema.json`
- `semantic-review-decision.v1.schema.json`
- `semantic-when-decision.v1.schema.json`
- `semantic-conflict-group.v1.schema.json`
- `semantic-batch-artifact.v1.schema.json`
- `semantic-parent-child-join.v1.schema.json`
- `semantic-mapping-basis.v1.schema.json`
- `snapshot-manifest.v1.schema.json`
- `snapshot-manifest.v1.1.schema.json`
- `snapshot-manifest-golden.v1.json`
- `SEMANTIC-SNAPSHOT-HASH-RECONCILIATION-20260904.json`

以下文件是离线验证入口：

- `tools/worldbook-migration/validate_migration_contract_v1_1.py`

没有通过 v1.1 validator 的批次只能是 `blocked` 或 `needs_review`，不能进入正式 Authoring、compile 或 publish。

## 3. 中间层对象和闭环

### 3.1 Source origin

每个旧输入原子必须生成一个 `origin_id`。允许的 `origin_kind`：

```text
file_metadata
field
array_item
keyword
rag
semantic_prototype
variant
when
text_mapping
runtime_signal
config_signal
adult_signal
parse_anomaly
entity_candidate
theme_candidate
external_reference
unknown_field
```

每个 origin 必须包含：

```text
origin_id
source_unit_id
origin_kind
origin_locator
raw_value_hash
destination.kind
destination.ids[]
disposition
rationale
loss[]
```

不变量：

- 每个 source atom 恰好一个 origin；
- `drop` 和 `unresolved` 必须有 loss；
- 一对多、多对一必须列出全部目标；
- `drop/unresolved` origin 不得进入 target span；
- 未识别字段进入 `unknown_field`，不得静默忽略；
- `SemanticPrototypes` 必须有 disposition。

### 3.2 Claim

每条 claim 必须包含：

```text
claim_id
epistemic_kind
subject
predicate
object
polarity: affirmed | negated | undetermined
perspective
time_scope
confidence
conflict_group_id
source_bindings[]
legacy_origin_ids[]
```

`source_bindings` 必须绑定：

```text
source_snapshot_id
source_root_id
source_id
source_version
source_content_hash
locator_root
relative_locator
normalized_quote
quote_hash
```

`source_content_hash` 必须等于 snapshot manifest 中对应文件的规范化内容 hash；source registry、snapshot 和 claim 三方不一致即阻断。

### 3.3 Target coverage

每个目标句/段必须有：

```text
target_span_id
target_locator
text_start
text_end
claim_ids[]
source_origin_ids[]
operation
unsupported_additions[]
inference_type
```

`operation` 只能是：

```text
preserve | merge | split | rephrase | drop | unresolved
```

双向不变量：

- target span 引用的 claim 必须存在；
- claim 引用的 origin 必须存在；
- 每个正文句/段至少有一个 claim，或明确标记 `unresolved`；
- `drop/unresolved` origin 不得被目标正文引用；
- source atom、claim、target span 的集合必须有完整 coverage；
- 每个目标 claim 的 polarity、perspective、time_scope 和 epistemic_kind 必须可追溯；
- 新增命题必须进入 `unsupported_additions` 或 `unresolved`，不得伪装为 fact。

### 3.4 Source atom coverage

“已盘点”与“已处置”是两个状态。只有以下条件同时满足，source unit 才能从 `inventory_complete` 进入 `semantic_in_progress`：

- 所有实际字段都在 allowlist 中，未知字段已成为 `unknown_field` origin；
- 每个数组项、Variant、When 分支和 TextMapping 都有独立 locator；
- runtime/config/adult/parse/theme/entity 信号都有对应 origin 或明确“不属于知识正文”的 disposition；
- `legacy_origins` 的 origin locator 集合与来源解析得到的 source atom 集合完全相等；
- 每个 origin 的 `raw_value_hash` 可从来源重新计算；
- 每个 origin 的 destination 和 disposition 均已填写；
- 任何遗漏、重复、越界或无法解析都使 unit 保持 `blocked`。

### 3.5 Parent/child snapshot join

父 snapshot、child manifest、source registry 和 candidate 不是互相独立的哈希标签，必须形成以下闭合关系：

```text
candidate.source_snapshot_id
  == worksheet.source_snapshot_id
  == child_manifest.source_snapshot_id

claim.source_binding.source_content_hash
  == child_manifest.files[relative_path].content_sha256

source_registry.source_content_hash
  == claim.source_binding.source_content_hash

child_manifest.parent_snapshot_id
  == recorded parent snapshot id
```

正式 join 记录必须同时保存：

```text
parent_snapshot_id
parent_snapshot_hash
child_snapshot_id
child_manifest_hash
source_root_id
source_version
registry_record_hash
```

任一项缺失、路径不在 child manifest、source hash 不匹配或 parent/child 关系未登记，都只能保持 `blocked`。

`source.registry.v1` 原有 schema 仍负责来源元数据格式；`semantic-parent-child-join.v1.schema.json` 负责迁移批次把来源登记、父 snapshot 和 child manifest 绑定起来。两者不能互相替代。

## 4. 认识论和关系规则

### 4.1 `source_fact`

来源明确声称的事实；不等于已经批准的世界正典。

### 4.2 `interpretation`

来源中的判断、因果解释、政治评价、战略评价或文化立场。必须保留 perspective。

### 4.3 `rumor`

来源明确标为传闻、据说、传说、民间记忆或敌对叙事的内容。不得升级为 `source_fact`。

### 4.4 `relationship`

是结构形态，不等于认识论类别。必须具有：

```text
subject_ref
predicate
object_ref
direction
time_scope
perspective
```

若实体尚未登记，端点必须是 unresolved mention，不能直接生成正式 entity ID。投影到 Authoring v1 时，`relationship` 映射为 `relation`，而非另造 assertion kind。

### 4.5 `state`

必须有 current/historical/bounded/unknown 时间范围。历史状态不得自动变成当前状态。

### 4.6 `polarity`

否定、未定和证据限制必须保留。目标格式不能承载 polarity 时，标记 `unsupported_for_v1` 并阻断，不得静默丢失。

## 5. When、权限和内容层

### 5.1 When 结算状态

每个旧 `When` 分支必须进入以下之一：

```text
audience_only
perspective_only
mapped_grant
mapped_deny
unresolved
rejected
```

默认是 `unresolved`，不是 grant。

只有同时满足以下条件才允许 `mapped_grant/mapped_deny`：

- profile/entity/condition registry ID 已确认；
- 旧条件含义已人工确认；
- `mapping_basis` 有 `basis_kind`、`registry_ids`、`legacy_origin_ids`、`review_state`、`rationale`；
- 不放大原始知识范围；
- deny 优先和空 grants/denies 语义已验证。

`unresolved` When 可以进入审阅候选，但不得进入 active expression、Runtime index 或 compile closure。

每个 When 分支必须生成一个结算记录，结果只能是：

```text
audience_only
perspective_only
mapped_grant
mapped_deny
unresolved
rejected
```

结算记录使用 `semantic-when-decision.v1.schema.json`，并要求：

- `unresolved` 的 When 不得进入 active expression；
- `mapped_grant/mapped_deny` 必须有 `mapping_basis`；
- `mapping_basis` 必须反向指向 registry ID 和具体 legacy origin；
- 空 grants/denies 的运行时结果固定为 `unknown`，不得解释为 public；
- When 中无法识别的字段必须成为 `unknown_field` 或 `unresolved` origin。

### 5.2 Content tier gate

`pending` 是中间层值，不能进入 Authoring v1。CLI 省略 `--content-tier` 的执行顺序固定为：

```text
CLI parse
→ tier resolution
→ graph closure
→ pre-write gate
```

只有已认证且闭包全为 `base` 时，省略参数才等价于 base。unknown/pending/adult_optional 必须在写入前阻断。

### 5.3 Permission gate order

权限相关的处理顺序固定为：

```text
读取旧 When
→ 拆分 audience/perspective/permission/state 语义
→ 绑定 registry-backed IDs
→ 生成 mapping_basis
→ 运行 deny/grant/detail golden cases
→ 才能产生 mapped_grant/mapped_deny
```

任何一步缺失，结果只能是 `unresolved`、`audience_only`、`perspective_only` 或 `rejected`，不得进入 active expression。

## 6. Provisional ID 和正式 ID

### 中间产物允许

```text
authoring_provisional.claim.<24 hex>
authoring_provisional.origin.<24 hex>
authoring_provisional.assertion.<24 hex>
authoring_provisional.expression.<24 hex>
authoring_provisional.document.<24 hex>
```

### 正式产物禁止

provisional ID 不得进入：

- Authoring v1 document/assertion/expression；
- source registry；
- entity/profile/referral registry；
- ID ledger；
- redirect/lifecycle；
- Runtime/package manifest；
- save key 或 runtime index。

正式投影必须：

1. 生成 final ID；
2. 经过 collision check；
3. 写入 ID ledger；
4. 产生 provisional → final 人工映射记录；
5. 使旧 review/candidate 在 source snapshot 变化时失效。

## 7. Snapshot canonicalization

### 7.1 文件规范化

- UTF-8；
- LF；
- no BOM；
- Unicode NFKC；
- `byte_length` 使用规范化 UTF-8 字节长度；
- 原始 hash 和规范化 hash 分开记录。

### 7.2 路径和排序

- 相对 source root；
- `\` 转 `/`；
- 路径比较键为 Unicode NFKC 后的 ordinal case-fold；
- 比较键碰撞直接阻断；
- 文件 entry 按比较键排序；
- 输出 path 使用规范化相对路径。

### 7.3 Hash

```text
file_entry = {
  "path": normalized_path,
  "byte_length": len(normalized_utf8_bytes),
  "content_sha256": lowercase_hex_sha256(normalized_utf8_bytes)
}

manifest_identity = {
  "normalization_version": "utf8-lf-no-bom-nfkc-v1",
  "source_root_id": source_root_id,
  "source_version": source_version,
  "files": sorted(file_entry[])
}

manifest_hash = SHA256(canonical_json_utf8(manifest_identity))
snapshot_hash = SHA256(
  "awake.worldbook.source-snapshot.v1\n"
  + manifest_hash + "\n"
  + source_root_id + "\n"
  + source_version
)
```

canonical JSON 规则：

- 递归对象键按 Unicode code point ordinal 排序；
- 数组顺序按契约指定，不重新排序语义数组；
- UTF-8 原字符，不 ASCII 转义；
- 数字使用 JSON 最小十进制表示；
- 无额外空白；
- hash 使用小写十六进制；
- `generated_at` 不进入 identity hash。

`SNAPSHOT-MANIFEST-GOLDEN-v1.json` 必须提供输入、manifest_hash 和 snapshot_hash，独立实现必须得到相同结果。

## 8. 人工审核状态机

### Decision object

```text
decision_id
candidate_id
candidate_hash
source_snapshot_id
source_snapshot_hash
reviewer_id
reviewer_role
decision
decision_at
checks
rejection_reason
supersedes_decision_id
```

规则：

- `accepted` 要求所有 semantic checks 为 true；
- `rejected` 要求非空 rejection_reason；
- `understood_rewrite=yes` 不能单独等同于 accepted；
- reviewer 必须与生成者角色分离；
- candidate hash 或 source snapshot 变化会使旧 decision 失效；
- 修订必须产生新 candidate revision，禁止原地覆盖；
- rejected 内容不可直接复用，除非新 candidate 明确声明 supersedes 并重新审阅。

## 9. 批次状态机和证据绑定

```text
planned
→ snapshot_locked
→ inventory_complete
→ semantic_in_progress
→ candidate_ready
→ review_required
→ accepted | blocked | rejected
→ compiled
→ published
→ runtime_verified
```

旁路终态：

```text
superseded
abandoned
```

每个状态必须绑定：

```text
batch_id
source_snapshot_id
artifact_hashes[]
evidence_level
recorded_at
next_action
```

parent snapshot、child snapshot、candidate 或 review revision 变化时，所有依赖它的下游证据自动变为 stale/invalidated。

合法状态传播：

```text
source change
→ snapshot_locked 失效
→ inventory/semantic/candidate/review evidence stale
→ 新 batch 或新 revision
```

```text
candidate change
→ candidate_ready/review_required 重新开始
→ 旧 review decision stale
```

```text
rejected
→ 不得进入 accepted/compiled/published
→ 只能通过新 revision + supersedes 重新审阅
```

`batch_complete` 不等于 `migration_complete`；必须分别报告：

```text
source_read
inventory_complete
semantic_claims_extracted
clustered
rewritten
semantic_reviewed
human_approved
compiled
published
runtime_verified
```

## 10. 重复、冲突和四档

### 重复分类

```text
duplicate
near_duplicate
variant_expression
perspective_conflict
epistemic_conflict
temporal_conflict
entity_conflict
tier_conflict
unresolved
```

相同 subject/predicate/object 但不同 perspective 默认是 `variant_expression` 或 `perspective_conflict`，不是自动 duplicate。

### 四档

本次单一下载权威包不使用四档版。若未来纳入四档：

- 每一档必须作为独立 source version 或明确 variant 输入；
- 不得跨档自动继承正文；
- clean/base 闭包不得通过 fallback、redirect、cache 或 index 间接吸收 adult/unknown；
- 档位差异必须有 tier join 记录；
- 缺失档位不得用另一档静默填充。

## 11. 语言、本地化和编码

每个目标语言字段必须记录：

```text
language
source_language
translation_or_rewrite_mode
provenance
review_state
```

必须检查：

- UTF-8/no BOM/LF；
- Unicode NFKC；
- title/summary/expression 的语言覆盖；
- 翻译不改变 polarity、视角、时间和命题类型；
- 占位符和专名一致；
- 缺失翻译不得用另一语言正文静默代替。

## 12. 可复用交付清单

- [ ] 权威 source root、parent snapshot、child snapshot 已绑定；
- [ ] 所有旧字段、数组项、异常和未知字段都有 origin；
- [ ] source atom → claim → target span 三向覆盖通过；
- [ ] polarity、主体/谓词/客体、视角、时间保持通过；
- [ ] `When` 已逐分支结算；
- [ ] 无证据 grant/deny 为零；
- [ ] provisional ID 未进入正式产物；
- [ ] pending/unknown 未被默认 base 绕过；
- [ ] snapshot golden fixture 复现通过；
- [ ] duplicate/conflict/tier 分类明确；
- [ ] 人工 decision 与 candidate/snapshot hash 绑定；
- [ ] batch state 和 E0–E5 evidence 绑定；
- [ ] 源 hash 前后不变；
- [ ] `needs_review`、approved、canon、compiled、published、runtime_verified 分开报告。

## 13. 当前试点的正确状态

当前 30 文件和五个候选只能说明：

- 已有来源快照和审计资料；
- 已有候选级语义重写；
- 仍需逐句覆盖审查和人工判断；
- source registry、权限、实体、时代和内容层未闭合；
- 不得扩大到全库迁移；
- 不得宣称 Runtime-ready。

方法论 v1.1 的目标不是让候选更快通过，而是让错误候选更早、可重复地被拒绝。
