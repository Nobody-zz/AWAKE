# Worldbook Studio AI 修复批次 2/3 checkpoint

> 日期：2026-09-06  
> 状态：`candidate_frozen / full_regression_verified`  
> 范围：候选身份/evidence 边界、review lifecycle、CAS 和幂等。

## 1. 已修改文件

- `tools/worldbook-studio/src/Awake.WorldbookStudio.Core/AuthoringDraftResponseNormalizer.cs`
- `tools/worldbook-studio/src/Awake.WorldbookStudio.Core/AuthoringLifecycleContracts.cs`
- `tools/worldbook-studio/src/Awake.WorldbookStudio.Core/AuthoringReviewDecisions.cs`
- `tools/worldbook-studio/src/Awake.WorldbookStudio.Web/AuthoringDraftEndpoints.cs`
- `tools/worldbook-studio/src/Awake.WorldbookStudio.Web/AuthoringDraftStore.cs`
- `tools/worldbook-studio/src/Awake.WorldbookStudio.Web/Program.cs`
- `tools/worldbook-studio/src/Awake.WorldbookStudio.Web/wwwroot/studio-draft.js`
- `tools/worldbook-studio/tests/Awake.WorldbookStudio.Draft.Tests/Program.cs`

## 2. 已实现

### Candidate/evidence

- normalized/approximate evidence 不再标记为完整 verified；
- quote hash 不匹配时产生 warning 并降级；
- CandidateSet 建立时拒绝重复 candidate/fact/expression identity；
- candidate fingerprint 参与生命周期绑定。

### Review lifecycle

- review 只允许操作当前 pending candidate；
- stale/superseded/discarded candidate 不允许继续审查；
- kept candidate 可进入建档；
- 单候选 create-document 路径也执行生命周期检查。

### CAS/idempotency

- review 请求支持 expected generation/source/packet baseline；
- operationId 支持重复操作 replay；
- operationId 绑定 request digest，复用到不同请求时返回 CAS conflict；
- legacy 请求无 operationId 时生成稳定兼容 ID；
- UI 审查操作期间锁定 busy 状态；
- UI 成功后用服务端 review projection 覆盖本地 CandidateSet。

## 3. 验证

```text
Release solution build: 0 warnings / 0 errors
DraftTests: 40/40 PASS
BatchTests: 21/21 PASS
Worldbook Studio harness: 113/113 PASS
Draft DOM/state: 2/2 PASS
Draft race: 2/2 PASS
Editor content: 7/7 PASS
```

## 4. 当前未完成

- warnings/unresolved/coverage 仍未成为一等 Draft/authoring 持久化字段；
- content tier 仍未贯通 create-document；
- subdomain/related domains 仍未从 candidate 保真到文档；
- certainty/inferred/perspective 仍未完整落盘；
- local draft 多任务隔离、timer 和服务端 Draft 续接未修复；
- Batch/cache 边界未修复；
- Quick Authoring UI 输入入口尚未增加；
- Semantic Migration 仍仅保持显式隔离，不在本批实现。

## 5. 下一步

进入批次 4：

```text
warnings/unresolved/coverage
content tier
subdomain/related domains
certainty/inferred/perspective provenance
```

