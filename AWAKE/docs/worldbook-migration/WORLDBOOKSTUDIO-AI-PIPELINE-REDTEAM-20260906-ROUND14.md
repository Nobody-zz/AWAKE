# Worldbook Studio AI 生成管线持续红队——第十四轮

> 日期：2026-09-06  
> 范围：AI Draft candidate 写入 authoring 文档后的 certainty、inferred、perspective 和证据语义保真。  
> 状态：`REVISE`  
> 本轮性质：只读静态攻击追踪 + Draft 离线基线；未修改实现。

## 1. 执行边界与基线

本轮未：

- 启动 Bannerlord；
- 同步游戏目录；
- 访问真实 Cloud Provider、API Key、Token、Worker 或网络；
- 读取/修改 Downloads 源目录；
- 修改五个世界书迁移候选；
- 修改 Worldbook Studio 实现。

本轮基线：

- DraftTests：`33/33 PASS`

## 2. 结论

| 严重度 | 数量 | 结论 |
|---|---:|---|
| P0 | 0 | 未发现直接进入 canon/runtime 的无权限路径 |
| P1 | 2 | 生成结果的认识论/推断语义和表达视角可能在落盘时丢失 |
| P2 | 1 | 建档 provenance 仍不足以解释所有生成字段 |

```text
PLAN_STATUS = NEEDS_REVISION
IMPLEMENTATION = NOT_APPROVED
CONTINUE REDTEAM
```

## 3. 新增发现

### RT-R14-01：fact 的 certainty 和 inferred 在 authoring assertion 中丢失

**严重度：P1**

**攻击输入**

```text
fact.kind = fact
fact.certainty = contested
fact.inferred = true
fact.review_status = accepted
```

**代码证据**

`AuthoringDraftContracts.cs:45-53` 明确包含：

```text
Certainty
Inferred
```

但 `AuthoringDraftDocumentBuilder.cs:37-48` 写入 assertion 时只保存：

```text
kind
text
author_created
expressions
```

没有把 `certainty` 和 `inferred` 写入 assertion 或其结构化 provenance。

**结果**

AI/作者在 Draft 审核阶段看到的：

```text
争议事实
AI 推断事实
不确定事实
```

进入 authoring 文档后可能只剩普通 `kind=fact` 和文本。后续作者无法区分：

```text
资料明确事实
争议说法
模型推断
```

这会削弱 Quick Authoring 的“风险可见”和人工复核要求，也可能让下游权限/预览逻辑把推断内容当作普通事实。

**修复要求**

- authoring assertion 保留 certainty/epistemic 状态；
- `inferred=true` 必须在文档中可见；
- 如果 authoring schema 不允许新增字段，写入结构化 author_created provenance；
- evidence support 与 certainty/inferred 不能只存在 Draft 内存；
- readback 必须与 candidate 的语义状态一致。

**回归用例**

```text
confirmed/contested/uncertain/inferred
→ create-document
→ readback 状态完整保留
```

---

### RT-R14-02：expression 的 perspective 和 inferred 在 authoring 文档中没有一等保真字段

**严重度：P1**

**攻击输入**

```text
expression.perspective = "普通平民"
expression.layer = "rumor"
expression.inferred = true
profile_ids = ["profile.commoner"]
```

**代码证据**

`AuthoringDraftDocumentBuilder.cs:62-74` 写入 expression 时保留：

```text
layer
text
grants
denies
```

但没有写入：

```text
perspective
inferred
fact_ids
```

`profile_ids` 只被转换为 grants，不能完全等价替代表达视角：

```text
perspective = 普通平民
profile_id = profile.commoner
```

两者可能不同，前者是写作/叙述视角，后者是权限身份。

**结果**

- Draft 中明确的 NPC 视角在落盘后可能只能从 grant 推断；
- `inferred=true` 的身份表达变成普通 expression；
- 后续作者无法判断这句话是资料表达、AI 推断还是作者改写；
- 多视角内容的保真审查被削弱。

**修复要求**

- 保留 perspective 和 inferred；
- 明确区分写作视角、适用身份和权限 grant；
- expression provenance 保存原始 fact_ids 与生成状态；
- authoring readback 能展示这些字段；
- 不允许用 profile grant 推断完整 perspective 语义。

**回归用例**

```text
不同 perspective、相同 profile_id → readback 不得合并
inferred=true → readback 必须可见
fact_ids 关系 → provenance 必须可追溯
```

---

### RT-R14-03：建档 provenance 只保存 evidence 和 candidate fingerprint，无法重放语义风险

**严重度：P2**

`AuthoringDraftDocumentBuilder.AuthorCreated` 当前 provenance 保存：

```text
source_content_hash
packet_hash
provider_fingerprint
candidate_fingerprint
evidence
```

但没有保存 candidate 级的：

```text
segmentation_reason_codes
source_spans
warnings
unresolved
coverage
certainty/inferred summary
perspective summary
```

因此 candidate 在 UI 中显示的解释与文档回读后的 provenance 不完整对应。

这不会直接绕过 needs_review 或 authority gate，但会让后续人工复审无法重建“为什么这条内容被这样生成和分组”。

## 4. 保留的正向边界

- 当前文档始终强制写入 `status=needs_review`；
- 表达的 profile grant 仍经过 registry 校验；
- Draft submission 仍校验 fact/expression ID 与证据绑定；
- DraftTests 当前 `33/33 PASS`；
- 未发现语义字段丢失可直接进入 canon/runtime 的路径。

## 5. 收敛状态

```text
P0 = 0
本轮新增 P1 = 2
累计待处置 P1 = 33
连续无新增 P0/P1 = 0 轮
下一步 = 继续红队或进入有授权的修复批次
```

