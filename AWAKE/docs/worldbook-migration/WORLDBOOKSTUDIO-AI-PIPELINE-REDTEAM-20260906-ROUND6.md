# Worldbook Studio AI 生成管线持续红队——第六轮

> 日期：2026-09-06  
> 范围：review decision 的 stale CAS、AI 诊断落盘、部分采纳语义和请求集合规范化。  
> 状态：`REVISE`  
> 本轮性质：只读静态攻击追踪 + 离线基线；未修改实现。

## 1. 执行边界与基线

本轮未：

- 启动 Bannerlord；
- 同步游戏目录；
- 访问 Cloud Provider、API Key、Token、Worker 或网络；
- 读取/修改 Downloads 源目录；
- 修改五个世界书迁移候选；
- 修改 Worldbook Studio 实现。

本轮基线：

- DraftTests：`33/33 PASS`
- BatchTests：`21/21 PASS`
- Draft DOM/state harness：`2/2 PASS`

## 2. 结论

| 严重度 | 数量 | 结论 |
|---|---:|---|
| P0 | 0 | 未发现直接绕过人工审核进入 canon/runtime 的路径 |
| P1 | 3 | stale 审查请求、诊断丢失和部分采纳语义可能造成错误建档 |
| P2 | 2 | 输入规范化和证据解释仍有可追溯性/资源边界问题 |

```text
PLAN_STATUS = NEEDS_REVISION
IMPLEMENTATION = NOT_APPROVED
CONTINUE REDTEAM
```

## 3. 新增发现

### RT-R6-01：review-decision 请求没有携带并校验客户端期望 generation

**严重度：P1**

**攻击步骤**

1. 客户端读取 CandidateSet generation `G1`；
2. 另一客户端或同一客户端完成 merge/split，使服务端变为 `G2`；
3. 第一客户端仍发送基于 `G1` 的 discard/reorder/merge；
4. `review-decision` 请求只提交 `draftId/operation/candidateIds/orderedCandidateIds/splitGroups`；
5. 服务端从 DraftStore 读取当前 CandidateSet 并直接执行操作，没有要求请求声明并匹配 `G1`。

**代码证据**

`AuthoringDraftEndpoints.cs` 的 review-decision handler 没有读取客户端 generation/fingerprint 字段；`AuthoringReviewDecisions.Validate` 只对服务端当前集合生成 decision 的 generation 字段。

**结果**

如果旧 candidate ID 仍存在于新集合中，stale 客户端操作可能被应用到新集合，而不是稳定返回 CAS 冲突。若该 candidate 已被标记 superseded/discarded，问题还会叠加前几轮发现的生命周期绕过。

**修复要求**

- 请求必须携带 `expected_generation_id`、`expected_packet_hash`、`expected_source_content_hash`；
- 服务端在执行前做严格匹配；
- 不匹配时返回稳定 `409`；
- UI 成功后以服务端投影重建本地状态；
- 旧客户端不能仅凭仍存在的 candidate ID 继续修改新状态。

**回归用例**

```text
G1 请求、服务端已变 G2 → 409，不产生 decision
G1 请求、服务端仍 G1 → 正常
candidate ID 保留但状态已 superseded → 仍按 generation/lifecycle 拒绝
```

---

### RT-R6-02：create-document 丢失 Draft 阶段 warnings/unresolved/coverage

**严重度：P1**

**攻击步骤**

1. Provider/Normalizer 对事实或候选生成 warning；
2. DraftStore 中保留结果；
3. 用户采纳事实和表达；
4. 调用 `create-document`；
5. `AuthoringDraftDocumentBuilder.Build` 只接收 title、summary、domain、facts、expressions、candidateSet；
6. 构建的 authoring 文档 provenance 只写入 source hash、packet hash、provider fingerprint、candidate fingerprint 和 evidence。

**结果**

即使前面的 DraftStore 已经修复为保留 warning，建档后作者档案仍可能丢失：

```text
warning
unresolved
coverage
segmentation reason
source span 的诊断状态
```

作者回读文档时无法知道 AI 哪些内容曾被标记为不确定，也无法继续处理未解决项。

**修复要求**

- 将 warnings/unresolved/coverage 持久化到 authoring provenance 或不可变 review record；
- create-document 前检查 blocking unresolved；
- 文档回读返回诊断摘要；
- resolved 必须有 decision ID 和解决依据；
- 不得以“文档仍是 needs_review”替代诊断信息保留。

**回归用例**

```text
warning 生成 → create-document → readback 仍可见
blocking unresolved → create-document 被拒绝
non-blocking unresolved → 可建档但明确标记
```

---

### RT-R6-03：complete 路径的部分采纳没有明确写入 selection 语义

**严重度：P1**

**攻击步骤**

1. complete 返回 facts、metadata、expressions；
2. 用户只采纳部分 facts；
3. 用户不采纳任何 expressions；
4. `studio-draft.js:draftCanCreate()` 仍允许创建；
5. 服务端 validator 允许空 expressions；
6. Builder 生成只有事实的 `needs_review` 文档。

**结果**

系统无法区分以下两种意图：

```text
作者明确只想创建事实草稿
作者以为“采纳草稿”会包含完整 candidate
```

这会造成内容静默缺失，尤其在快速生成入口中更容易误导用户。

**修复要求**

- 明确 `partial` 与 `full` 建档模式；
- complete + 未处理表达时显示未采纳数量；
- 若产品要求完整候选，未完成表达时阻断；
- 若允许部分建档，把事实/表达 selection summary 写入 provenance；
- 建档按钮文案必须反映实际提交范围。

**回归用例**

```text
complete + 只采纳 facts → partial 明示或 422
complete + facts/expressions 全部采纳 → full
```

---

### RT-R6-04：acceptedFacts 的非 accepted 项会被静默过滤

**严重度：P2**

**攻击输入**

向 `AuthoringDraftRequestFactory.Create` 传入：

```text
[accepted fact A, pending fact B]
```

**代码证据**

`AuthoringDraftContracts.cs:136`：

```csharp
(acceptedFacts ?? []).Where(IsAcceptedFact).Take(64).ToArray()
```

**结果**

pending 项不进入 request body，也不产生错误或诊断。调用方无法区分：

```text
用户只提交了 A
客户端误把 B 一起提交但系统静默丢弃
```

对于新 Quick Authoring 意图集合，这会让请求 hash 与调用方提交状态不一致。

**修复要求**

- acceptedFacts 集合中出现非 accepted 项时在服务端拒绝；
- 或返回明确的 dropped item 列表并阻断生成；
- 超过数量上限也必须拒绝而不是 `Take` 截断；
- 请求 hash 只对显式确认的规范化输入计算。

---

### RT-R6-05：用户可编辑 fact text，但 evidence 仍显示为完整支持

**严重度：P2**

**攻击步骤**

1. AI 返回事实和 evidence；
2. 作者编辑 fact text，加入 source quote 中没有的命题；
3. `AuthoringDraftSubmissionValidator` 允许编辑 text，只要求 evidence/ID/binding 不变；
4. Builder 将编辑后的 text 写入 assertion，同时继续写原 evidence。

**结果**

当前行为是有意允许人工编辑，但文档没有区分：

```text
AI 原始事实文本
作者修订文本
修订文本是否仍被原 evidence 完整支持
```

这会在快速流程中制造“证据支持已验证”的误解。

**修复要求**

- 保留 `generated_text` 与 `author_edited_text`；
- 编辑后自动降级 evidence support 为 `needs_review`；
- 或要求作者重新确认“编辑内容可能超出原文依据”；
- 不得把 quote hash 未变解释成 proposition 仍完整受支持。

## 4. 保留的正向边界

- 未发现无人工操作直接进入 canon/runtime 的路径；
- 预先 accepted 的 AI 输出仍被 parser 拒绝；
- 事实/表达 ID、evidence 和 profile binding 仍有服务端校验；
- Draft、Batch 和 DOM 基线全部通过。

## 5. 收敛状态

本轮新增 P1 尚无实现处置证据：

```text
P0 = 0
本轮新增 P1 = 3
累计待处置 P1 = 16
连续无新增 P0/P1 = 0 轮
下一步 = 继续红队或先修复已发现缺口
```

