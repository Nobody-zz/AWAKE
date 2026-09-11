# Worldbook Studio AI 生成管线持续红队——第四轮

> 日期：2026-09-06  
> 范围：Worldbook Studio Draft/Complete 的审查回读、候选权威性、UI 状态和分阶段风险保留。  
> 状态：`REVISE`  
> 本轮性质：只读静态攻击追踪 + 现有离线回归基线；未修改实现。

## 1. 执行边界与基线

本轮只检查 Worldbook Studio：

- `AuthoringReviewDecisions`
- `AuthoringDraftStore`
- `AuthoringDraftEndpoints`
- `AuthoringDraftSubmissionValidator`
- `AuthoringDraftDocumentBuilder`
- `studio-draft.js`
- Draft/Batch/Web 既有状态边界

本轮没有：

- 启动 Bannerlord；
- 同步游戏目录；
- 访问真实 Cloud Provider、API Key、Token、Worker 或网络；
- 读取、修改 Downloads 源目录；
- 修改五个世界书迁移候选；
- 修改实现代码。

当前离线基线：

- DraftTests：`33/33 PASS`
- BatchTests：`21/21 PASS`
- Worldbook Studio harness：`113/113 PASS`

这些测试通过不覆盖本轮新增攻击的组合路径。

## 2. 本轮结论

本轮未发现无需人工操作即可发布或写入运行时的 P0 路径，但发现以下新增 P1/P2：

| 严重度 | 数量 | 结论 |
|---|---:|---|
| P0 | 0 | 未发现直接绕过人工审核进入 canon/runtime 的路径 |
| P1 | 4 | 可造成权威候选错用、审核风险丢失或 UI/服务端状态分裂 |
| P2 | 2 | 削弱诊断可见性或保留语义，但不直接绕过发布门 |

最终状态：

```text
PLAN_STATUS = NEEDS_REVISION
IMPLEMENTATION = NOT_APPROVED
CONTINUE REDTEAM
```

## 3. 新增发现

### RT-R4-01：superseded 候选仍可被 create-document 选中

**严重度：P1**

**攻击链**

```text
候选 A + 候选 B
→ merge A/B
→ A、B 被标记 superseded
→ 旧 candidate_id 仍留在 CandidateSet
→ create-document(candidate_id=A)
```

**代码证据**

`AuthoringReviewDecisions.cs:116-145` 在 merge/split 后把源候选标记为：

```text
ReviewStatus = "superseded"
```

但源候选仍保留在 `CandidateSet.Candidates` 中。

`AuthoringDraftEndpoints.cs:142-145` 调用 `SelectCandidate`，只按 candidate ID 查找，没有拒绝：

```text
superseded
stale
discarded
EvidenceCurrent = false
```

`AuthoringDraftSubmissionValidator` 只检查事实和表达是否属于 selected result，并没有检查所选候选的生命周期状态。

**结果**

客户端可以继续提交一个已经被 merge/split 取代的候选，服务端可能把它作为新的 `needs_review` 作者草稿写入。虽然仍未直接进入 canon，但审查决定被绕过，用户看到的“合并后不再使用原候选”不成立。

**修复要求**

- `SelectCandidate` 只允许可建档状态；
- `superseded`、`discarded`、`stale` 和 `EvidenceCurrent=false` 必须稳定拒绝；
- `keep/kept` 是否可建档必须由明确状态表决定；
- create-document 错误应返回稳定的 candidate lifecycle conflict；
- CandidateSet 必须保存“可建档候选”与“历史审查候选”的区别。

**回归用例**

```text
merge → 原候选 create-document = 409
split → 被 supersede 的候选 create-document = 409
discard → discarded 候选 create-document = 409
invalidate → stale 候选 create-document = 409
derived candidate → 只有当前可建档候选可提交
```

---

### RT-R4-02：review-decision 成功后 UI 不替换权威 CandidateSet

**严重度：P1**

**攻击链**

```text
UI 发送 merge/split/discard/reorder
→ 服务端返回 authoritative reviewProjection
→ 前端只追加 materialized_candidates
→ 前端保留旧 candidates 数组和旧状态
```

**代码证据**

`studio-draft.js:255-263`：

```text
const materialized = ...
if (operation === "discard") ...
if (materialized.length) draftState.candidates.push(...materialized)
```

前端没有使用响应中的 `reviewProjection.candidates` 替换 `draftState.candidates`。

**结果**

- merge 后源候选在 UI 仍可能显示为 pending；
- split 后旧候选和 derived 候选同时存在，但生命周期状态不一致；
- reorder 的服务端规范顺序可能不反映到 UI；
- UI 选中的 candidate、详情和实际服务端权威 CandidateSet 分裂；
- 用户可能再次点击已经 superseded 的候选并得到 409，或在不同界面看到相互矛盾的状态。

**修复要求**

- review-decision 成功后以服务端 `reviewProjection` 或完整 CandidateSet 为唯一回读源；
- 不要在客户端自行推断 superseded/derived 状态；
- 保留当前选中项时，按稳定 candidate ID 在新集合中恢复；
- 如果当前选中项已失效，自动清除并显示原因；
- response 中的 generation/packet/source binding 必须重新写入前端状态。

**回归用例**

```text
merge → UI 与 GET review-projection 完全一致
split → 旧候选 superseded、derived 候选可见
discard → discarded 状态回读一致
reorder → 服务端顺序与 UI 顺序一致
```

---

### RT-R4-03：前一阶段 warnings 在后续阶段被覆盖

**严重度：P1**

**攻击链**

```text
facts 生成 warning
→ metadata 生成 warnings=[]
→ expressions 生成 warnings=[]
→ DraftStore.SaveResult
→ 当前 warnings 覆盖旧 warnings
```

**代码证据**

`AuthoringDraftStore.cs:221-232` 重新构造 `AuthoringDraftResult` 时使用当前 `result.Warnings`，没有合并前一阶段的 warning 或保存 warning lineage。

**结果**

事实阶段的 evidence 风险、无法定位引用等重要信息可能在继续生成后消失。作者最终只看到当前阶段的结果，无法知道此前发生过什么。

**修复要求**

- warning 必须按 stage/scope 持久化；
- 后续阶段只能追加、标记 resolved 或 superseded；
- resolved 必须记录解决理由和关联 ID；
- candidate-level warning 不得落在全局字符串数组中而失去归属。

**回归用例**

```text
facts warning
→ metadata
→ expressions
→ complete/readback 仍可看到该 warning 或其 resolved 记录
```

---

### RT-R4-04：前端允许在缺少表达采纳时创建“完整”草稿

**严重度：P1**

**攻击链**

```text
complete 返回 facts + expressions
→ 用户只采纳 fact
→ draftCanCreate() 返回 true
→ create-document(expressions=[])
→ 生成只有事实、没有身份表达的 needs_review 档案
```

**代码证据**

`studio-draft.js:229` 的 `draftCanCreate()` 只要求：

```text
至少一个 accepted fact
title
summary
domain
```

它不要求：

```text
complete candidate 的表达已处理
blocking warning 已解决
candidate lifecycle 可建档
```

服务端 validator 允许空表达列表，因此“完整生成”路径可以静默变成“只写事实”路径。

**结果**

用户以为自己采纳了完整候选，但实际档案丢失身份表达。对有意只创建事实档案的用户来说这不一定是错误；问题在于 UI 没有明确显示这是“部分建档”还是“完整候选建档”。

**修复要求**

- 明确两种操作：`创建部分事实草稿` 与 `创建完整候选草稿`；
- complete 模式下未处理表达必须显示数量和状态；
- 如果产品要求完整候选，表达未完成时阻断；
- 如果允许部分建档，写入 provenance 的 selection summary；
- 不得让按钮文案“采纳草稿”掩盖实际只提交了部分内容。

**回归用例**

```text
complete + 只采纳 facts → 明确 partial 或阻断
complete + facts/expressions 均采纳 → full needs_review
```

---

### RT-R4-05：候选审查成功后的前端详情仍可能读取旧 source spans

**严重度：P2**

**攻击链**

```text
merge/split 返回新的 source_spans
→ UI 只 push derived candidate
→ 当前 candidateDetailId 未随权威投影重建
→ 详情区域继续展示旧 candidate 的 spans
```

**结果**

用户可能把旧候选的来源定位误认为新合并/拆分候选的解释。它不直接写入 canon，但破坏证据追溯。

**修复要求**

- 详情只从当前权威 CandidateSet 查找；
- candidate 被 supersede 后自动刷新或关闭详情；
- source span 展示必须带 candidate fingerprint/generation；
- stale 详情不得继续显示“已验证”。

---

### RT-R4-06：AI 结果的空 candidates 与顶层事实投影语义不一致

**严重度：P2**

**攻击链**

```text
complete 返回 candidates=[]
同时返回顶层 facts/metadata/expressions
→ Normalizer 接受两套字段
→ Store 按 stage 保存顶层字段
→ UI 可能显示“没有候选”但仍能创建顶层草稿
```

**结果**

“未形成可靠候选”和“有一个兼容顶层结果”之间没有稳定产品语义，用户可能绕过候选审查路径。

**修复要求**

- complete 模式冻结互斥结构：
  - candidates 非空时顶层内容必须为空；
  - candidates 为空时明确 outcome/reason；
- 空 candidates + 非空顶层内容应拒绝或转为显式 legacy result；
- UI 不得把空候选当作可直接建档的完整结果。

## 4. 本轮保留的正向结论

现有实现仍保留以下有效边界：

- Provider 返回预先 accepted 的事实/表达会被 parser 拒绝；
- create-document 对未知事实 ID、证据篡改和表达孤立引用有校验；
- CandidateSet 的 review decision 有 source hash、packet hash 和 generation CAS；
- `needs_review` 文档不会自动变成 canon；
- Draft/Batch/Studio 现有回归基线均通过。

## 5. 必须加入方案的修订项

1. 候选生命周期状态表和 create-document 允许状态；
2. review-decision 后客户端完整回读权威 CandidateSet；
3. warnings 的 stage/scope/lineage/resolution 模型；
4. complete 路径的 partial/full 建档语义；
5. 空 candidates 与顶层投影的互斥规则；
6. source span 详情与 candidate fingerprint 绑定；
7. 对上述组合攻击增加 HTTP/UI 回归。

## 6. 收敛状态

本轮新增 P1 未有实现处置证据，因此不能计入红队收敛轮次。

```text
P0 = 0
新增 P1 = 4
连续无新增 P0/P1 轮次 = 0
下一步 = 继续红队或先修复本轮 P1
```

