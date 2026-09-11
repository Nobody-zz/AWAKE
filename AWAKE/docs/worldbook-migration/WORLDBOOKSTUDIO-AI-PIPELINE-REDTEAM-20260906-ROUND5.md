# Worldbook Studio AI 生成管线持续红队——第五轮

> 日期：2026-09-06  
> 范围：审查操作的重复提交/并发、derived candidate 证据归属、UI 权威回读与生命周期状态。  
> 状态：`REVISE`  
> 本轮性质：只读静态攻击追踪 + 离线基线；未修改实现。

## 1. 执行边界与测试基线

本轮没有：

- 启动 Bannerlord；
- 同步游戏目录；
- 访问真实 Cloud Provider、API Key、Token、Worker 或网络；
- 读取/修改 Downloads 源目录；
- 修改五个世界书迁移候选；
- 修改 Worldbook Studio 实现。

本轮离线基线：

- DraftTests：`33/33 PASS`
- BatchTests：`21/21 PASS`
- Draft DOM/state harness：`2/2 PASS`

基线通过不能证明本轮重复审查操作和生命周期组合已经安全。

## 2. 本轮结论

| 严重度 | 数量 | 结论 |
|---|---:|---|
| P0 | 0 | 未发现直接进入 canon/runtime 的无权限路径 |
| P1 | 4 | 会造成重复决策、权威集合歧义或证据错误归属 |
| P2 | 1 | 会造成局部 UI 诊断残留或解释不一致 |

```text
PLAN_STATUS = NEEDS_REVISION
IMPLEMENTATION = NOT_APPROVED
CONTINUE REDTEAM
```

## 3. 新增发现

### RT-R5-01：review-decision 缺少操作幂等键，重复提交可重复物化候选

**严重度：P1**

**攻击步骤**

1. 用户选中两个候选；
2. 连续双击“合并选中”，或在网络延迟时重复提交同一请求；
3. 两个请求都包含相同 `draftId/operation/candidateIds`；
4. 服务端没有 operation idempotency key；
5. `AuthoringDraftStore.SaveReviewDecision` 每次追加 decision；
6. `AuthoringReviewDecisions.Apply` 每次都生成 derived candidate 并追加到 CandidateSet。

**代码证据**

- `AuthoringDraftEndpoints.cs` 的 `review-decision` 请求没有 operation ID；
- `AuthoringDraftStore.SaveReviewDecision` 直接追加 decision；
- `AuthoringReviewDecisions.Apply` 使用 `Concat(materialized)` 追加派生候选；
- 前端 `studio-draft.js` 的 `draftReviewDecision` 没有 busy/decision lock。

同样的 merge 输入会生成相同 fingerprint/candidate ID，造成重复候选或后续 `ToDictionary`/选择歧义。

**修复要求**

- 每个 review decision 必须有客户端生成的 operation ID；
- 服务端以 `draftId + operationId + candidateSetGenerationId` 幂等；
- 相同操作重放原响应，不追加第二条 decision；
- UI 在请求期间禁用审查操作；
- 新候选 fingerprint 已存在时不得重复追加。

**回归用例**

```text
同一 merge 请求提交两次 → 相同响应、一个 decision、一个 derived candidate
并发 merge → 一个成功，另一个 replay/409
重复 split/reorder/discard → 不重复写 ledger
```

---

### RT-R5-02：derived split candidate 复制全部 source spans，证据归属失真

**严重度：P1**

**攻击步骤**

1. 一个 candidate 包含两个事实，各自对应不同 source span；
2. 用户执行 split；
3. `AuthoringReviewDecisions.Apply` 为每个分组调用 `CreateDerivedCandidate`；
4. 每个 split candidate 都传入：

```csharp
selected[0].SourceSpans ?? []
```

而不是按事实分组过滤 source spans。

**代码证据**

`AuthoringReviewDecisions.cs:105-112`：

```text
groupFacts
groupExpressions
selected[0].SourceSpans
```

**结果**

拆分后的每个候选都会显示原候选全部 source spans。作者无法判断某个 span 是否真正支持该拆分出来的命题，证据追溯发生污染。

**修复要求**

- source span 必须带 fact/proposition 关联；
- split 时按事实/表达引用过滤 span；
- 无法确定归属时标记 `unresolved`/`unverified`；
- 不得因为 span 在原 candidate 中存在就复制到每个 derived candidate。

**回归用例**

```text
两事实、两 span 的 candidate split
→ 每个 derived candidate 只保留对应 span
→ 无归属 span 进入 warning/unresolved
```

---

### RT-R5-03：审查后 UI 继续使用本地旧候选集，服务端权威状态未覆盖

**严重度：P1**

**攻击步骤**

1. UI 提交 merge/split/discard/reorder；
2. 服务端返回 `reviewProjection` 和新的 authoritative CandidateSet；
3. 前端只执行：

```javascript
if(materialized.length) draftState.candidates.push(...materialized)
```

4. 不替换原候选集，不应用服务端的 superseded/discarded/kept/reordered 状态。

**结果**

- UI 仍显示已 superseded 候选为可用；
- UI 顺序与服务端顺序不一致；
- candidate detail 可能继续展示已失效 source spans；
- 下一次 create/review 请求使用旧本地状态，形成用户与服务端状态分裂。

**修复要求**

- 成功响应中的 `reviewProjection.candidates` 必须覆盖本地候选集合；
- 当前选中项按 candidate ID 恢复；
- 已 superseded/stale 的选中项必须清除；
- UI 展示状态必须由服务端投影驱动，不能本地猜测。

**回归用例**

```text
merge/split/discard/reorder 后：
UI candidate 列表 == GET review-projection
UI candidate 状态 == 服务端状态
UI detail 不显示已失效候选
```

---

### RT-R5-04：重复审查可在 superseded 候选上继续生成 derived candidate

**严重度：P1**

**攻击步骤**

1. merge A/B 后，A/B 变为 `superseded`；
2. 服务端仍把 A/B 留在 CandidateSet；
3. 再次调用 review-decision，选择 A/B；
4. `AuthoringReviewDecisions.Validate` 只检查 ID 是否存在，不检查生命周期状态；
5. `Apply` 再次基于 superseded 候选生成 merge/split 结果。

**结果**

- 审查历史出现基于已取代候选的后续决策；
- CandidateSet 可能出现多条互相矛盾的 derived candidate；
- review ledger 无法表达“该决策针对当前有效候选还是历史候选”。

**修复要求**

- review operation 只能接受当前可操作状态；
- `superseded/discarded/stale` 候选只能用于历史回看；
- 历史候选如需重新处理，必须显式 clone/revive 并生成新 generation；
- `Validate` 和 `SelectCandidate` 共用同一生命周期规则。

**回归用例**

```text
merge 后再次 merge 原 A/B → 409
discard 后 split 原候选 → 409
stale 后任何 review operation → 409
```

---

### RT-R5-05：split 后 source span 详情可能残留旧候选，造成局部 UI 误导

**严重度：P2**

**攻击步骤**

1. UI 当前打开 candidate A 的 detail；
2. 执行 split；
3. 前端追加 derived candidate，但不覆盖/清理 `candidateDetailId`；
4. detail 仍按旧 ID 查找并继续展示旧 spans。

**结果**

用户可能把旧候选的全部 source spans 误认为新拆分候选的证据。

**修复要求**

- 审查响应后重新建立 detail；
- 失效候选 detail 自动关闭；
- detail 必须显示 candidate fingerprint/generation；
- source span 显示必须从当前 authoritative projection 获取。

## 4. 本轮保留的正向边界

- 未发现直接跳过人工审核进入 canon/runtime 的路径；
- 既有 parser 会拒绝预先 accepted 内容；
- submission validator 仍校验证据、事实 ID、表达绑定；
- authority gate 仍独立于 Quick Authoring；
- Draft/Batch/DOM 基线测试全部通过。

## 5. 需要加入方案的修订项

1. review decision operation idempotency；
2. CandidateSet candidate lifecycle 状态矩阵；
3. superseded/stale/discarded 的 review/create 双重拒绝；
4. split 的 source span 归属算法；
5. review response 覆盖本地 CandidateSet；
6. derived candidate fingerprint 去重；
7. candidate detail 与 generation/fingerprint 绑定。

## 6. 收敛状态

本轮新增 P1 仍未有实现处置证据，因此不能计入无新增收敛轮次。

```text
P0 = 0
本轮新增 P1 = 4
累计待处置 P1 = 13
连续无新增 P0/P1 = 0 轮
下一步 = 继续红队或先修复本轮 P1
```

