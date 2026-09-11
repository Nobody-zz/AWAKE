# Worldbook Studio 批量作者 Revision 13 独立审查包

- task_id: `WORLDBOOK-STUDIO-BATCH-AUTHORING-20260824`
- contract: `2026-08-24-worldbook-studio-batch-authoring-contracts.v2.json`
- revision: `13`
- contract_sha256: `217E7538DC7C356D092ABC029A79E7504EE36BC1000B0B3FD3F7FF261FDCB4A9`
- packet_scope: 只审查是否可以进入离线批量实现；不审查游戏运行、发布同步或冻结候选。
- reviewer_side_effects: 只读；不得改文件、启动游戏、同步目录或调用外部 Provider。

## 机械证据

以下两条命令必须分别通过：

```powershell
& "...\tools\worldbook-studio\scripts\batch-contract-check.ps1"
& powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "...\tools\worldbook-studio\scripts\batch-contract-check.ps1"
```

当前结果：`BATCH CONTRACT CHECK: PASS (1130/1130)`；两个 PowerShell 引擎均为 exit `0`。

## 审查不变量

### 1. 批次分配与恢复

- `reserved → allocation_pending → promoting → committed` 使用稳定 `allocation_id`。
- `batch_id` 一旦持久化不得重新分配；promotion journal 是恢复权威，scan projection 不是恢复权威。
- owner fence 过期后只能通过 claim-rebind CAS 接管；`claim_generation` 递增；旧 fence/generation 的提交必须拒绝。
- 通用 batch journal 与 promotion journal 使用不同状态枚举；中断必须映射到 `failed` 或 `needs_reconcile`，并保留 batch_id。

### 2. 事实、元数据与 consent

- 事实审核成功后进入 `metadata_pending(active_attempt_stage=none,result_stage=facts)`，不代表 Provider 已运行。
- `start(stage=metadata,target_item_ids=...)` 必须验证目标非空、属于授权集合、属于当前 batch 且全部满足 stage 条件；目标 CAS 必须全量成功，否则任何 item 都不改变。
- metadata 调度顺序唯一为：目标全量 CAS → start-schedule journal commit → consent `issued → consumed` CAS → Provider call。
- Provider result commit 不再次消费 consent；部分失败/unknown 子集必须重新授权，并只能通过 `retry-item(stage=metadata)` 回到 `metadata_pending`。
- metadata attempt/result/cache materialization 必须绑定服务端重算的 accepted fact set hash；旧事实集不得提交新元数据。

### 3. 证据与缓存

- 每条 fact 必须绑定 evidence；evidence 同时绑定 batch/item/result/snapshot/content hashes、locator、quote、attempt 和 binding hash。
- locator、quote、evidence binding、result、metadata selection、accepted fact set 使用契约规定的 canonical JSON/NFC/LF/SHA-256 公式。
- cache hit 必须有 `attempt_kind=cache_hit` 和 batch-local materialization；不能直接复用旧 batch 的 evidence/result ref。
- cache entry、payload、commit marker 三文件共享 `cache_key`；只有 published marker 且三者校验一致才可命中，否则 quarantine/reconcile。

### 4. 公开边界与作者草稿

- public projection 不得泄露 `document_path`、reservation 内部路径、workspace marker、owner instance、lease/fence、provider secret、原始异常文本或 raw `last_error`。
- item-detail 必须能公开读取 facts/evidence/source-unit 和 metadata candidates。
- create-documents 只能创建 `needs_review` 作者草稿；禁止自动发布正典、expressions、身份/人物/家族绑定。
- `expressions`、identity/person/family binding 在 V1 直接硬拒绝。

### 5. API 与门禁

- 每条 route 的 request/response/error 必须绑定 Revision 13 registry schema；写操作需要 origin/CSRF/owner 边界。
- scan 结果位于 `prebatches/<scan_id>`；create 通过 promotion 原子进入 `batches/<batch_id>`。
- 路径拒绝 rooted、反斜杠、`.`、`..`、空段、设备路径、reparse point 和受保护游戏/发布目录。
- BatchTests 必须由 `scripts/test.ps1` 调用，并由 `scripts/release-check.ps1` 汇总；当前实现阶段可尚未存在，但不能改变上述契约门禁。

## 独立审查结论格式

审查代理必须返回：`status`、`verdict`、`p0`、`p1`、`p2`、`evidence`、`files_read`、`files_changed`、`limitations`、`next_action`。

只有在当前契约 hash 与本文件一致、双引擎机械门禁通过、且没有 P0/P1 时，才允许输出精确终端行：

```text
VERDICT: APPROVED
```

`REVIEW_ERROR`、超时、摘要性“看起来没问题”或只通过机械检查，均不构成批准。
