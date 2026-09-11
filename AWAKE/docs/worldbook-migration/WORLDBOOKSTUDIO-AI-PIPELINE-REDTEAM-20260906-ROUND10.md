# Worldbook Studio AI 生成管线持续红队——第十轮

> 日期：2026-09-06  
> 范围：Provider/Batch 缓存失效、共享 Prompt revision、无 evidence 事实的建档边界。  
> 状态：`REVISE`  
> 本轮性质：只读静态攻击追踪 + Draft/Batch/DOM 离线基线；未修改实现。

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
- BatchTests：`21/21 PASS`
- Draft DOM/state harness：`2/2 PASS`

## 2. 结论

| 严重度 | 数量 | 结论 |
|---|---:|---|
| P0 | 0 | 未发现直接进入 canon/runtime 的无权限路径 |
| P1 | 2 | Prompt 版本与 Batch cache 可能失配；无 evidence 事实仍可能被提交建档 |
| P2 | 1 | Batch 语义降级和 Quick Authoring 边界需要显式记录 |

```text
PLAN_STATUS = NEEDS_REVISION
IMPLEMENTATION = NOT_APPROVED
CONTINUE REDTEAM
```

## 3. 新增发现

### RT-R10-01：共享 Authoring Prompt revision 变化不会自动失效 Batch cache

**严重度：P1**

`BatchProviderService` 的 cache key 固定包含：

```text
prompt_revision = worldbook.authoring-draft.prompt.v2
output_schema_revision = awake.worldbook.batch-fact-result.v2
```

但它实际调用共享的 `AuthoringDraftProviderFactory` 和 `AuthoringDraftRequestSerializer`。如果 Quick Authoring 修改系统 Prompt、请求字段或输出约束，而没有同步更新 Batch 的 cache revision，旧 Batch cache 仍可能被当成当前 Prompt 的结果复用。

**攻击步骤**

1. 使用旧 Prompt v2 生成并缓存 Batch facts；
2. 修改共享 Prompt，增加 Quick intent 或语义约束；
3. 不修改 Batch cache revision；
4. 重新运行 Batch；
5. cache 命中，跳过新的 Provider 生成。

**结果**

- Batch 结果与当前 Prompt 不一致；
- 新的 safety/semantic constraint 没有作用于旧缓存；
- 用户无法从 cache entry 看出结果对应哪一份完整系统 Prompt；
- Quick 与 Batch 的语义边界可能因共享 revision 而被错误耦合。

**修复要求**

- Quick Authoring 和 Batch 使用独立 prompt revision；
- 或将完整 prompt hash/contract hash 纳入各自 cache key；
- 修改共享 serializer 时必须使 Batch cache 失效；
- cache entry 记录 mode、stage、prompt revision 和 output schema revision；
- 不得用同一个 revision 字符串代表两种不同语义路径。

**回归用例**

```text
修改 Quick Prompt → 旧 Batch cache 不命中
Batch Prompt 不变 → cache 可复用
不同 mode/stage → cache key 必须不同
```

---

### RT-R10-02：缺失 evidence 的 fact 可通过 parser、validator 并进入 needs_review 文档

**严重度：P1**

**攻击输入**

Provider 返回：

```json
{
  "id": "fact-no-evidence",
  "kind": "fact",
  "text": "资料中没有明确写出的新事实",
  "certainty": "confirmed",
  "inferred": false,
  "evidence": null,
  "review_status": "pending"
}
```

**代码证据**

- `AuthoringDraftResponseNormalizer.NormalizeFacts` 允许 `evidence` 为空；
- `AuthoringDraftResultParser.ParseFacts` 使用 `ParseEvidence`，但不要求非空；
- `AuthoringDraftSubmissionValidator` 在 generated/submitted evidence 都为空时允许提交；
- `AuthoringDraftDocumentBuilder.EvidenceFor` 对无 evidence 的 fact 返回空集合。

**结果**

该 fact 可以经过：

```text
Provider
→ Normalizer
→ Parser
→ 人工标记 accepted
→ create-document
→ needs_review authoring document
```

虽然仍未进入 canon，但系统的 Quick Authoring 输出可能显示它是普通 fact，而不是明确的“无证据待核对项”。这违反了“完整候选继续显示 evidence/warnings/unresolved”的目标，并给人工审核造成错误信号。

**修复要求**

二选一，但必须明确：

1. fact evidence 缺失直接阻断 parser；或
2. 保留为 review-only，但强制生成：

```text
support_status = unresolved
blocking = true
```

并禁止 create-document，直到人工补充/确认来源。

不能仅靠 `review_status=pending` 表示证据缺失。

**回归用例**

```text
evidence=null → blocking unresolved
blocking unresolved → create-document=422
有效 evidence → 正常
expression evidence=null 但绑定有效 fact → 按独立规则处理
```

---

### RT-R10-03：Batch TranslateFacts 可把 source 不支持的文本保留为普通 fact

**严重度：P2**

`BatchProviderService.TranslateFacts` 在 evidence 无法直接定位时会使用整个 source unit 作为 fallback evidence，并把结果降为 yellow/manual review。

这保护了不自动发布的状态，但没有独立区分：

```text
fact text 被 source unit 完整支持
fact text 只与 source unit 同处一个范围
fact text 与 source unit 语义无关
```

如果后续 Batch UI 或文档流程把 yellow fact 当作普通事实继续处理，source-unit fallback 会产生比实际更强的证据印象。

**修复要求**

- fallback evidence 明确标记 `scope_only`；
- 不得显示为 quote verified；
- Batch public projection 显示“仅核对范围，不是事实支持证据”；
- Batch 结果继续明确 `mode=batch_facts`，不升级为 Quick candidate。

## 4. 保留的正向边界

- Batch cache 有 result hash、commit marker 和 key payload 校验；
- Batch facts 仍需后续审核；
- Draft parser 拒绝 preaccepted 结果；
- 现有服务端 create-document 不直接写 canon；
- Draft/Batch/DOM 基线全部通过。

## 5. 收敛状态

```text
P0 = 0
本轮新增 P1 = 2
累计待处置 P1 = 25
连续无新增 P0/P1 = 0 轮
下一步 = 继续红队或进入有授权的修复批次
```

