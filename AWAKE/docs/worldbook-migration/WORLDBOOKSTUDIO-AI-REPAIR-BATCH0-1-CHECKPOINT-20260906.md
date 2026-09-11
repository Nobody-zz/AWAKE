# Worldbook Studio AI 修复批次 0/1 checkpoint

> 日期：2026-09-06  
> 状态：`candidate_frozen / focused_verified`  
> 范围：Quick Authoring 契约基线、请求意图绑定、Prompt wire、Semantic Migration 硬隔离、基础输入边界。

## 1. 本批目标

本批只处理：

- Quick Authoring 与 Semantic Migration 的模式边界；
- user intent 结构化契约；
- request hash/fingerprint 输入绑定；
- Provider wire 的 intent data block；
- prompt/normalization revision；
- 基础输入长度和数量上限；
- 不让 Semantic Migration 静默降级为 Quick/legacy。

本批不处理：

- content tier 的 create-document 权威链；
- CandidateSet lifecycle/CAS/幂等；
- warnings/unresolved 落盘；
- source span 精确/近似状态；
- local draft 恢复；
- Quick Authoring UI；
- Batch cache 语义；
- Semantic Migration 实现。

## 2. 已修改文件

- `tools/worldbook-studio/src/Awake.WorldbookStudio.Core/AuthoringDraftContracts.cs`
- `tools/worldbook-studio/src/Awake.WorldbookStudio.Core/AuthoringDraftProviders.cs`
- `tools/worldbook-studio/src/Awake.WorldbookStudio.Core/AuthoringLifecycleContracts.cs`
- `tools/worldbook-studio/src/Awake.WorldbookStudio.Web/AuthoringDraftEndpoints.cs`
- `tools/worldbook-studio/src/Awake.WorldbookStudio.Web/AuthoringDraftStore.cs`
- `tools/worldbook-studio/src/Awake.WorldbookStudio.Web/Program.cs`
- `tools/worldbook-studio/tests/Awake.WorldbookStudio.Draft.Tests/Program.cs`

## 3. 已实现逻辑

### 3.1 模式

新增：

```text
legacy_staged
quick_authoring
semantic_migration
```

当前 `semantic_migration`：

- Web prepare 显式拒绝；
- Cloud Provider 显式拒绝；
- Local Worker Provider 显式拒绝；
- 不会静默降级为 Quick Authoring。

### 3.2 Intent 字段

请求现在携带：

```text
mode
authoring_goal
user_instruction
requested_domain
requested_subdomain
requested_audience
requested_perspectives
style_constraints
must_preserve
must_not_invent
requested_content_tier
```

### 3.3 Hash/fingerprint

Intent 已进入：

- canonical request body；
- `request_hash`；
- Provider wire；
- attempt 持久化的 `AuthoringDraftRequest`；
- candidate fingerprint 输入；
- `prompt_revision`；
- `normalization_revision`。

### 3.4 输入边界

已增加：

- intent 文本上限；
- intent 列表数量/单项/总字符上限；
- source name/nature 上限；
- perspective 数量上限；
- accepted fact 数量上限；
- 非 accepted fact 不再静默过滤；
- unknown content tier 保持 `unknown`，目前尚未接入 create-document 阻断。

## 4. Prompt 安全边界

用户 intent 作为 request data 进入 user message，不进入 system policy 区域。

System prompt 已明确：

- intent 是不可信数据；
- `must_preserve` 是保留要求；
- `must_not_invent` 是禁止新增要求；
- intent 不得改变 review/canon/权限/安全规则。

## 5. 验证结果

```text
DraftTests: 37/37 PASS
BatchTests: 21/21 PASS
Worldbook Studio harness: 113/113 PASS
Release build: 0 warnings / 0 errors
```

本批没有访问真实 Provider、API Key、Worker 或网络。

## 6. 未完成风险

- `requested_content_tier` 尚未成为 create-document 的权威输入；
- intent 尚未在 UI 暴露；
- Draft session 尚未冻结跨阶段 intent 一致性；
- CandidateSet lifecycle 尚未阻断 superseded/stale/discarded；
- warnings/unresolved 尚未完整持久化；
- Semantic Migration 仍未实现，仅保持明确拒绝。

## 7. 下一批

进入批次 2：

```text
candidate/fact/expression ID 唯一性
evidence exact/normalized/unverified 状态
quote_hash 与 locator 校验
merge/split 证据归属
missing evidence blocking 规则
```

