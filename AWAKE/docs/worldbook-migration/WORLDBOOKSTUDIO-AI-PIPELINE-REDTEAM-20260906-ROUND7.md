# Worldbook Studio AI 生成管线持续红队——第七轮

> 日期：2026-09-06  
> 范围：DraftStore 持久化、重启恢复、旧状态兼容、legacy candidate migration 和错误信息边界。  
> 状态：`REVISE`  
> 本轮性质：只读静态攻击追踪 + Draft/Batch 离线基线；未修改实现。

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

## 2. 结论

| 严重度 | 数量 | 结论 |
|---|---:|---|
| P0 | 0 | 未发现直接进入 canon/runtime 的无权限路径 |
| P1 | 3 | 持久化兼容和 legacy 恢复可能错误解释或覆盖 Draft 状态 |
| P2 | 2 | 错误信息和损坏状态处理仍有泄露/诊断边界风险 |

```text
PLAN_STATUS = NEEDS_REVISION
IMPLEMENTATION = NOT_APPROVED
CONTINUE REDTEAM
```

## 3. 新增发现

### RT-R7-01：Draft state 没有持久化 schema/version 迁移门

**严重度：P1**

`AuthoringDraftStore` 的 `PersistedState` 只有：

```text
Drafts
Consents
Attempts
Revision
```

没有明确的：

```text
schema_version
contract_version
intent_version
candidate_model_version
```

**攻击方式**

构造旧版状态文件，缺少未来 Quick Authoring 字段，随后启动新版 Studio。

**风险**

`System.Text.Json` 会把新增字段静默当作 null/default。新版代码可能把旧 Draft 当成新的 Quick Draft 继续处理，从而出现：

- mode 缺失却被默认解释；
- user intent 缺失却继续生成；
- content tier 缺失却沿用默认；
- warnings/unresolved 缺失却被视为“没有风险”。

**修复要求**

- 持久化状态必须有 schema version；
- 旧版本必须显式迁移、标记 `legacy_staged` 或阻断；
- 缺失 Quick Authoring intent 时不得静默当作完整 Quick 请求；
- state migration 必须保留原始状态 hash 和迁移记录。

---

### RT-R7-02：重复 Draft/Attempt 记录会静默覆盖

**严重度：P1**

`ReloadStateUnsafe` 使用：

```csharp
_drafts[draft.DraftId] = draft;
_attempts[attempt.AttemptId] = attempt;
```

**攻击方式**

构造合法 JSON，但包含两个相同 `draftId` 或 `attemptId` 的记录，第二条内容不同。

**结果**

后读取的记录静默覆盖前一条，没有：

- duplicate ID 错误；
- 状态文件 quarantine；
- 冲突证据；
- 用户可见恢复提示。

这会导致 attempt request hash、result、status 或 retry lineage 被悄悄替换。

**修复要求**

- 加载时检测重复 stable ID；
- 重复记录直接 quarantine 并返回稳定 CAS/状态损坏错误；
- 不得依赖 JSON 数组顺序决定权威记录；
- 每条记录应有 immutable record hash 或 revision 绑定。

---

### RT-R7-03：Legacy candidate migration 缺少结果/请求/source 一致性检查

**严重度：P1**

`MigrateLegacyCandidatesUnsafe` 会从最新 attempt 找候选并调用：

```csharp
AuthoringLifecycleFactory.FromDraftResult(
    attempt.Request with { Stage = Complete },
    draft.Result,
    attempt.ProviderFingerprint)
```

但在迁移前没有完整验证：

```text
attempt.Result.RequestHash == draft.Result.RequestHash
attempt.Result.SourceContentHash == draft.SourceContentHash
attempt.Result.Stage 与 draft.Result.Stage 的预期关系
attempt.Result.CandidateSet 是否来自同一 generation
```

**攻击方式**

让同一个 Draft 的历史 attempt 拥有不同 request/result，随后触发 Studio 重启和 legacy migration。

**风险**

旧候选可能被重建到当前 Draft，形成：

- 错误 candidate fingerprint；
- 错误 provider lineage；
- source hash 与 candidate 事实不一致；
- stale candidate 被恢复成可审查 candidate。

**修复要求**

- migration 前执行完整 binding/CAS 校验；
- 任一 hash 或 generation 不一致时标记 `legacy_migration_conflict`；
- 不得自动恢复为当前 CandidateSet；
- legacy migration 结果默认 stale/needs_manual_reconcile。

---

### RT-R7-04：状态损坏错误与 attempt error_message 可能暴露过多内部信息

**严重度：P2**

`AuthoringDraftAttempt` 持久化：

```text
ErrorCode
ErrorMessage
ResultUnknown
```

`GetAttemptStatus` 又将 `error_message` 返回给客户端。

**风险**

Provider/Normalizer 异常可能包含：

- 模型生成的未知字段值；
- 用户输入片段；
- source-specific locator；
- 内部路径或解析上下文；
- 外部 Provider 返回的错误正文。

当前错误映射多数安全，但新 Quick Authoring 字段增加后，直接保存和回读完整异常会扩大泄露面。

**修复要求**

- 持久化 safe error code、短摘要和 correlation ID；
- 原始错误仅写受限本地诊断日志；
- 公共 attempt status 不返回完整 exception message；
- Provider response body 不进入 Draft state。

---

### RT-R7-05：损坏状态 quarantine 后，启动流程可能只保留文件移动而不生成恢复指引

**严重度：P2**

状态读取失败时当前逻辑会尝试将文件移动为：

```text
state.v1.json.corrupt-{timestamp}
```

但缺少结构化恢复记录，用户难以知道：

- 哪个 Draft 丢失；
- 哪个 attempt 处于 unknown；
- 是否可以安全重试；
- 是否需要人工检查旧文件。

**修复要求**

- 生成结构化 recovery report；
- 标记受影响 Draft/Attempt；
- 明确 `manual_required`；
- 不自动把未知状态重新变成 failed 或成功。

## 4. 保留的正向边界

- DraftStore 已有跨进程 revision CAS；
- attempt 状态有 prepared/running/succeeded/failed/unknown；
- retry 对 session、draft、stage、request hash、source hash 和 provider fingerprint 有校验；
- 现有 Draft/Batch 基线测试通过；
- 未发现持久化异常直接绕过人工审核进入 canon/runtime 的路径。

## 5. 方案必须新增的修订项

1. Draft state schema version 和 migration policy；
2. 重复 stable ID 的 fail-closed 加载；
3. legacy candidate migration 的完整 binding 校验；
4. safe error projection 与原始诊断隔离；
5. 状态损坏 recovery report；
6. Quick Authoring intent 缺失时的 legacy_staged 语义。

## 6. 收敛状态

本轮新增 P1 尚无实现处置证据：

```text
P0 = 0
本轮新增 P1 = 3
累计待处置 P1 = 19
连续无新增 P0/P1 = 0 轮
下一步 = 继续红队或先修复已发现缺口
```

