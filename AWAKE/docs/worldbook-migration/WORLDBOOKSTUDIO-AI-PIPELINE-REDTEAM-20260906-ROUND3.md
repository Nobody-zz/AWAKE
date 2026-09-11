# Worldbook Studio AI 生成管线持续红队——第三轮

> 日期：2026-09-06  
> 范围：Worldbook Studio AI Draft/Complete 生成链路；承接前两轮 Quick Authoring 方案红队。  
> 状态：`REVISE`  
> 本轮性质：只读静态攻击追踪 + 现有离线回归基线；未修改实现。

## 1. 边界与执行声明

本轮只检查：

- `AuthoringDraftContracts`
- `AuthoringDraftProviders`
- `AuthoringDraftResponseNormalizer`
- `AuthoringDraftSubmissionValidator`
- `AuthoringDraftDocumentBuilder`
- `AuthoringDraftStore`
- Draft/Batch/Web 现有边界

本轮没有：

- 启动 Bannerlord；
- 同步游戏目录；
- 访问 Cloud Provider、API Key、Token 或真实 Worker；
- 读取或修改 Downloads 源目录；
- 修改五个世界书迁移候选；
- 修改 Worldbook Studio 实现代码。

基线回归：

- DraftTests：`33/33 PASS`
- BatchTests：`21/21 PASS`
- Worldbook Studio harness：`113/113 PASS`

基线通过只证明现有结构、状态和既有 fixture 没有回归，不等于本轮攻击面已经关闭。

## 2. 本轮结论

前两轮已经确认 Quick Authoring 方案存在契约、Prompt、语义、tier、UI、兼容和并发缺口。

本轮新增确认：

| 严重度 | 数量 | 结论 |
|---|---:|---|
| P0 | 0 | 未发现无需人工权限即可发布或进入运行时的路径 |
| P1 | 5 | 会造成审核信息丢失、事实错绑、请求完整性弱化或候选歧义 |
| P2 | 2 | 会削弱审计可追溯性或证据质量，但当前不直接绕过发布门 |

最终状态仍为：

```text
PLAN_STATUS = NEEDS_REVISION
IMPLEMENTATION = NOT_APPROVED
```

## 3. 新增发现

### RT-R3-01：分阶段生成会覆盖前一阶段 warnings

**严重度：P1**

**攻击前提**

1. `facts` 阶段返回无法定位的 evidence；
2. Normalizer 产生 evidence warning；
3. 作者继续生成 `metadata` 或 `expressions`；
4. 后续阶段返回空 warnings。

**攻击输入**

```text
source = "哈尔达尔继承王位。"
facts result = quote 无法定位，warnings 非空
metadata result = warnings = []
```

**代码证据**

`src/Awake.WorldbookStudio.Web/AuthoringDraftStore.cs:221-232`

`SaveResult` 构造新的 `AuthoringDraftResult` 时直接使用当前阶段的：

```text
Warnings = result.Warnings
```

没有合并 `previous` 或 `selectedPrevious` 的 warnings。

**结果**

上一阶段的证据风险可能在继续工作后消失。作者查看当前 Draft 时，曾经提示“引用无法定位”的风险不再显示。

**违反的契约**

完整候选必须持续显示：

```text
evidence / warnings / unresolved
```

**修复要求**

- warnings 按阶段和 scope 持久化；
- 后续阶段只追加或明确 supersede，不得静默覆盖；
- candidate-level warning 必须绑定 candidate；
- warning 被解决时记录解决依据，而不是直接删除。

**回归用例**

```text
facts warning
→ metadata generate
→ expressions generate
→ warning 仍可回读
```

---

### RT-R3-02：accepted facts 超过 64 条会被静默截断

**严重度：P1**

**攻击输入**

向 metadata 或 expressions 阶段提交 65 条均为 `accepted` 的事实。

**代码证据**

`src/Awake.WorldbookStudio.Core/AuthoringDraftContracts.cs:136`

当前请求工厂执行：

```csharp
var facts = (acceptedFacts ?? []).Where(IsAcceptedFact).Take(64).ToArray();
```

**结果**

- 调用方可以认为 65 条事实已经被接受；
- 实际 request body 只包含前 64 条；
- 没有 warning、错误或截断标记；
- Provider 生成 metadata/expressions 时看不到最后一条事实；
- request hash 对应的是被截断后的请求，而不是用户提交的完整状态。

这不是“输入超限被拒绝”，而是静默改变用户意图。

**修复要求**

- accepted facts 超过上限时在 prepare 阶段阻断；
- 或明确生成分页/批次协议；
- 禁止使用 `Take` 静默丢弃用户已采纳内容；
- request hash 必须覆盖用户实际提交的规范化全集。

**回归用例**

```text
64 accepted facts → pass
65 accepted facts → stable 413/422
request body 不得静默缩短
```

---

### RT-R3-03：缺失 expression.fact_ids 时会静默绑定唯一 accepted fact

**严重度：P1**

**攻击输入**

在只有一条 accepted fact 的 expressions 请求中，Provider 返回：

```json
{
  "id": "expr-1",
  "text": "有人这样说。",
  "profile_ids": ["profile.commoner"],
  "fact_ids": []
}
```

**代码证据**

`src/Awake.WorldbookStudio.Core/AuthoringDraftResponseNormalizer.cs:280-284`

当前逻辑：

```csharp
if (factIds.Count == 0 && request.AcceptedFacts.Count == 1)
    factIds.Add(request.AcceptedFacts[0].Id);
```

**结果**

Provider 没有提供事实绑定，Normalizer 却自动补上唯一事实。这样会把“模型没有明确说明依据”的表达伪装成已绑定表达。

当唯一 accepted fact 与表达语义不匹配时，后续 validator 仍然看到合法的 `fact_id`，无法区分：

```text
模型明确绑定
模型遗漏绑定后被系统补绑定
```

**修复要求**

- expressions 输出缺失 `fact_ids` 时生成格式错误或 unresolved；
- 不得用唯一事实做隐式绑定；
- 若保留兼容行为，必须记录 `binding_recovered=true` 并阻断 create-document，等待人工确认；
- Quick Authoring 不能用默认绑定冒充证据绑定。

**回归用例**

```text
fact_ids 缺失 + 一个 accepted fact → 不得自动 accepted
fact_ids 明确且唯一 → 正常
fact_ids 多条或未知 → 拒绝
```

---

### RT-R3-04：缺失 request/source binding 字段会被补成当前请求值

**严重度：P1**

**攻击输入**

Provider 返回内容时省略：

```json
request_hash
source_content_hash
review_only
```

但保留事实和表达内容。

**代码证据**

`src/Awake.WorldbookStudio.Core/AuthoringDraftResponseNormalizer.cs:79-81`

当前逻辑：

```csharp
NormalizeHash(source["request_hash"], request.RequestHash, "request_hash")
NormalizeHash(source["source_content_hash"], request.SourceContentHash, "source_content_hash")
NormalizeBoolean(source["review_only"], true, "review_only")
```

缺失字段会被静默填入：

```text
request.RequestHash
request.SourceContentHash
true
```

**结果**

- Provider 未遵守 response binding contract 仍可被接受；
- 审计无法区分“Provider 回显了绑定值”和“Normalizer 替它补值”；
- 真实 Provider/Worker 配置错误会被隐藏；
- 对 Quick Authoring 新增 intent 字段后，这种 fallback 可能掩盖意图未被 Provider 处理。

当前结果仍是 review-only，因此未发现 P0 发布绕过；但它削弱了请求完整性证明。

**修复要求**

- 对 request_hash、source_content_hash、review_only 采取 fail-closed；
- 缺失字段直接返回稳定格式错误；
- 仅允许明确标注的旧 v1 兼容分支使用 fallback；
- fallback 必须产生结构化 warning，并不得进入“已验证绑定”状态。

**回归用例**

```text
缺 request_hash → reject
缺 source_content_hash → reject
缺 review_only → reject
字段存在且与请求一致 → pass
```

---

### RT-R3-05：重复 candidate ID 未被拒绝，可能造成审查歧义

**严重度：P1**

**攻击输入**

Provider 返回两个候选：

```json
[
  {"id": "candidate.same", "...": "..."},
  {"id": "candidate.same", "...": "..."}
]
```

或两个重试结果经过归一化后拥有相同逻辑 ID。

**代码证据**

- `src/Awake.WorldbookStudio.Core/AuthoringDraftResponseNormalizer.cs:90-121`
- `src/Awake.WorldbookStudio.Core/AuthoringDraftContracts.cs:331-350`

候选解析会读取 `id`，但没有在候选集合层执行唯一性校验。

后续选择逻辑：

`src/Awake.WorldbookStudio.Web/AuthoringDraftStore.cs:471-483`

使用 `FirstOrDefault` 按 ID 取候选。

**结果**

- UI 可以展示两个看似不同的候选；
- 选择同一个 ID 时只会取第一条；
- review decision 的 candidate ID 无法唯一指向一个候选；
- retry 去重和 candidate fingerprint 可能产生重复显示或错误回读。

**修复要求**

- parse/normalize 阶段拒绝重复 candidate ID；
- candidate fingerprint 必须在集合中唯一；
- retry 相同 fingerprint 必须变成 replay，不追加候选；
- review decision 必须按唯一稳定 ID 操作。

**回归用例**

```text
重复 candidate ID → reject
不同 ID、相同 payload → replay/deduplicate
不同 fingerprint → 保留并记录 retry lineage
```

---

### RT-R3-06：candidate explanation 在建档后没有完整持久化

**严重度：P2**

**攻击输入**

一个完整 candidate 包含：

```text
segmentation_reason_codes
source_spans
evidence
```

作者完成采纳并调用 `create-document`。

**代码证据**

`src/Awake.WorldbookStudio.Core/AuthoringDraftDocumentBuilder.cs:123-149`

建档 provenance 持久化了：

```text
source_content_hash
packet_hash
provider_fingerprint
candidate_fingerprint
evidence
```

但没有持久化：

```text
segmentation_reason_codes
source_spans
```

**结果**

候选审查页面可以看到分割解释，但生成的 authoring 文档回读后只剩 candidate fingerprint 和证据集合，无法独立复核：

```text
为什么这样分割
原文跨度是什么
跨度是否验证
```

**修复要求**

- 如果这些字段是 Quick Authoring 的审查证据，应写入 authoring provenance；
- 或明确它们只属于 Draft session，并在文档中写入不可变审查记录引用；
- 不能在 UI 展示“有解释”，而落盘后完全丢失。

---

### RT-R3-07：evidence reference_id/locator 可被模型伪造

**严重度：P2**

**攻击输入**

Provider 返回：

```json
{
  "reference_id": "official.archive.volume-99",
  "locator": "page 999",
  "quote": "确实存在于当前 source 的一段文字"
}
```

**代码证据**

`src/Awake.WorldbookStudio.Core/AuthoringDraftResponseNormalizer.cs:342-350`

只要 quote 能在当前 source 中定位，Normalizer 会保留 Provider 提供的 `reference_id` 和 `locator`，并将 evidence 标为 verified。

当前 Draft request 没有冻结的 source registry 来校验这些标识。

**结果**

逐字 quote 本身可能是真的，但来源名称和定位可能是模型编造的。下游作者会误以为引用来自官方档案，而实际只有用户粘贴的 source 被定位。

**修复要求**

- 区分 `quote_verified` 与 `reference_identity_verified`；
- 没有 source registry 时，reference_id/locator 只能标记为 `untrusted_label`；
- UI 不得把它显示为官方来源；
- source registry 存在时才允许标记完整 verified。

## 4. 本轮未发现的安全绕过

本轮仍未发现：

- 未经人工采纳直接进入 `create-document` 的稳定路径；
- `review_only=false` 能绕过当前 parser/validator 并进入 canon 的路径；
- 直接写入 approved/canon/compiled/published 的 Quick Authoring 路径；
- Batch 直接接管 Draft CandidateSet 的现成路径。

但这些结论不能替代新增 P1 的修复。

## 5. 必须加入正式方案的修订项

进入实现前，正式方案至少要增加：

1. warnings/unresolved 的阶段合并和 supersede 规则；
2. accepted facts 超限的阻断策略；
3. 禁止 expression.fact_ids 的隐式唯一事实绑定；
4. request/source/review binding 字段缺失时 fail-closed；
5. candidate ID/fingerprint 集合唯一性；
6. candidate explanation 的落盘或审查记录引用；
7. `quote_verified` 与 `reference_identity_verified` 的分离；
8. 每个新增 fallback 都必须有兼容版本和结构化 warning；
9. 新增 Quick Authoring 负向组合测试；
10. 不把 DraftTests 当前全绿解释为语义安全已经完成。

## 6. 通过条件

本轮不通过。要达到红队收敛，必须满足：

```text
P0 = 0
P1 = 0，或每项有用户明确接受的残余风险记录
新增 P1 均有回归测试
连续两轮独立红队没有新增 P0/P1
```

本轮结论：

```text
CONTINUE REDTEAM
```
