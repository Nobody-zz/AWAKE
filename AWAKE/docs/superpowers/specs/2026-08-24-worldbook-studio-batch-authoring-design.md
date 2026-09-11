# Worldbook Studio 批量制作设计

- date: 2026-08-24
- status: revision_13_pending_independent_review
- authority: 本文与 `2026-08-24-worldbook-studio-batch-authoring-contracts.v2.json` 的 Revision 13 为本批次唯一设计契约；旧 Revision 1–12 段落不再作为规范。
- scope: Worldbook Studio V1 批量事实整理、人工审核、元数据候选和 needs_review 作者草稿

## 目标

让非开发者内容编辑者可以一次导入多份参考资料，由 AI 分批提取带可验证原文依据的客观事实，人工审核后生成标题、摘要、主分类和二级分类候选，并按选择创建 `needs_review` 作者草稿。

AI 不得自动进入正典，不得自动覆盖旧档案，不得修改游戏目录或运行时世界书。

## V1 范围

包含：

- 文件/文件夹批量上传、扫描、规范化和章节/主题切分；
- 每个资料单元独立提取客观事实；
- 服务端生成和验证来源 snapshot、locator、quote 与证据 sidecar；
- 绿色/黄色/红色人工审核队列；
- 标题、摘要、主分类、二级分类候选；
- 人工选择后复用现有作者草稿路径创建 `needs_review` 档案；
- 任务暂停、取消、人工重试、断点恢复、缓存、报告和跨进程幂等。

不包含：

- 批量生成身份表达或调用 `expressions` 阶段；
- 自动发布正典、自动覆盖旧档案、自动裁决冲突；
- 人物/家族/地点代码自动绑定；
- 自动联网抓取、CLI 批量入口、游戏目录同步、游戏内读取器；
- 跨资料语义合并。V1 只产生确定性的 `duplicate_hint`：规范化事实文本完全相同的项目互相提示，不合并、不删除、不改变事实。

## 契约文件

机器可读字段、枚举、必填字段和 API 载荷见：

`docs/superpowers/specs/2026-08-24-worldbook-studio-batch-authoring-contracts.v2.json`

实现必须以该文件和本文为准；字段冲突时以契约文件的字段名/枚举为准，以本文的提交顺序和失败语义为准。

## 工作区布局与所有权

批量扫描在生成 `batch_id` 之前使用独立的预批次目录；预批次不会进入作者列表、Snapshot、Validate、Compile 或 Export。

```text
<workspace>/prebatches/
  create-reservations/<idempotency_key>.json
  <scan_id>/
    scan.json
    sources/<snapshot_id>.txt
    sources/<snapshot_id>.meta.json
    sources/<snapshot_id>.units/<source_unit_id>.json
    promotion/<operation_id>.json
    quarantine/...

<workspace>/batches/<batch_id>/
  manifest.json
  items/<item_id>.json
  items/<item_id>.attempts/<attempt_id>.json
  items/<item_id>.result.json
  items/<item_id>.metadata.json
  items/<item_id>.review.json
  sources/<snapshot_id>.txt
  sources/<snapshot_id>.meta.json
  sources/<snapshot_id>.units/<source_unit_id>.json
  evidence/v1/<evidence_id>.json
  consents/<consent_id>.json
  cache-materializations/<materialization_id>.json
  idempotency/<idempotency_key>.json
  journal/<operation_id>.json
  quarantine/<operation_id>/...

<workspace>/cache/v1/<cache_key>.json
<workspace>/cache/v1/<cache_key>.payload.json
<workspace>/cache/v1/<cache_key>.commit.json
```

`scan`、snapshot 和 source unit 先写入 `prebatches/<scan_id>/`。`create` 先在 workspace 级 `prebatches/create-reservations/<idempotency_key>.json` 原子保存 canonical input hash、scan、请求参数、服务端解析出的 Provider fingerprint 和既有 `operation_id`。分配 `batch_id` 后，先 durable 写入包含完整 `scan_id → batch_id → operation_id` 映射的 promotion journal `state=prepared`，再以 reservation CAS 写入 `state=promoting` 与相同 `batch_id`/`operation_id`，两者都持久化后才允许创建 `batches/<batch_id>.tmp`。同 key 的并发请求在 reservation 未完成时返回可重试的 `WB-BATCH-IDEMPOTENCY-INFLIGHT-409`，不创建第二个 batch。随后复制/校验所有文件并原子重命名为 `batches/<batch_id>`；只有 promotion journal 提交完成后 manifest 才进入 `planned`，`scan.promotion.state` 只能作为 journal 的投影。promotion 失败或进程中断时按 journal 记录的 batch_id/operation_id 完成、隔离或标记 `needs_reconcile`，不得凭目录猜测恢复；恢复唯一读取 `prebatches/<scan_id>/promotion/<operation_id>.json`。

只有 `BatchRepository` 可以访问 `prebatches/` 与 `batches/`；通用 `WorkspaceService`、`ListDocuments`、`Snapshot`、`Validate`、`Compile`、`WriteCompiled` 和 `Export` 必须显式拒绝或跳过这两个目录。批量文件不是世界书档案、来源档案或运行时输出。

`WorkspaceWritePolicy` 必须提供 `RequirePrebatches` 与 `RequireBatches`，初始化时建立上述目录。所有路径都必须经过 workspace root、reparse point、设备路径、路径段和原子写入检查；相对路径拒绝空段、`.`、`..`、反斜杠、绝对路径和设备路径。

## 规范化与稳定哈希

所有进入哈希、切分和证据定位的文本统一为 UTF-8、去 BOM、Unicode NFC、LF 换行。`normalized_content_hash` 是该文本的 SHA-256；`raw_content_hash` 是上传字节解码前的 SHA-256。

批量 canonical JSON 规则固定为：对象键按 ordinal 排序，字符串 NFC，数组保持顺序；集合字段先按元素 canonical JSON 字节排序；缺省数组为 `[]`，缺省可选字符串为 `""`，缺省可选对象为 `{}`，数字使用不带本地化的 JSON 数字格式，布尔和 null 使用 JSON 原生值。规范化后的 UTF-8 字节用于 SHA-256。

固定哈希公式：`unit_hash` 使用 `SHA-256(CanonicalJson({end_utf16,line_end,line_start,normalized_content_hash,start_utf16,text}))`；`scan_hash` 使用 `SHA-256(CanonicalJson({normalization_revision,snapshot_bindings:sort([{snapshot_id,snapshot_hash}]),splitter_revision}))`；`accepted_fact_set_hash` 使用排序后的事实 ID、文本、kind、确定程度、风险等级、source unit 和 evidence ID 集合；`consent_binding_hash` 使用 authorized item 集合、batch、owner、claim_generation、Provider、发送范围、选定的 `{snapshot_id,snapshot_hash}` 对集合和 workspace marker hash；`result_hash` 统一使用 `SHA-256(CanonicalJson(result_payload_without_result_hash_and_created_at))`；机器契约中的同名公式为唯一权威。集合字段排序规则见机器契约。

## 文件上传、扫描与 snapshot

浏览器使用 `multipart/form-data` 上传文件，不提交本机绝对路径。允许扩展名：`.txt`、`.md`、`.yaml`、`.yml`、`.json`。

服务器只接受相对文件名：统一使用 `/`，拒绝 `..`、`.` 空段、驱动器前缀、UNC/设备路径、NUL、空文件名、绝对路径、reparse point 和 Unicode NFC 后的大小写冲突。文件名按 NFC 处理，Windows 不区分大小写时发生碰撞即拒绝。

请求体上限 70 MiB；最多 200 个文件；单文件原始大小上限 16 MiB；批量总原始大小上限 64 MiB；单个切分资料单元上限 80,000 字符。服务器流式写入临时 snapshot，同步计算 hash，写入完成后再执行规范化、切分和扫描 hash。

上传成功后才创建 server-owned `snapshot_id` 和持久 `scan_id`。scan 保存 `owner_id`、`workspace_marker_hash`、snapshot hash 集合、scan hash、created_at、expires_at；`created_owner_instance_id` 仅作审计；默认 24 小时过期。扫描失败时临时文件进入 quarantine，不能留下可再次处理的半成品。

## Journal 提交与崩溃恢复

每个跨文件操作先写通用 journal，字段为：`operation_id`、`operation_type`、`batch_id`、`item_id`、`phase_seq`、`commit_seq`、`state`、`targets`、`previous_hashes`、`next_hashes`、`previous_revisions`、`next_revisions`、`created_at`、`updated_at`。通用 journal 的 `state` 为 `prepared`、`committed`、`aborted`、`quarantined`、`needs_reconcile`。预批次 promotion 另有独立 `promotion_journal`，其阶段状态为 `prepared`、`copying`、`verified`、`renamed`、`committed`、`aborted`、`quarantined`、`needs_reconcile`；二者不能互相代替。

所有文件使用同目录临时文件、写入后 `Flush(true)`、原子替换。提交顺序固定：

1. 导入：source snapshot → source metadata → item checkpoint → manifest `commit_seq`；
2. 提取：item result → evidence sidecar → item accepted state → manifest summary；
3. 创建档案：idempotency reservation → authoring 临时档案 → 原子档案提交 → sidecar linkage → item `created` → manifest summary。

恢复时按 `commit_seq` 和 `phase_seq` 检查目标：目标存在且 hash 匹配视为该阶段已提交；临时文件移入 quarantine；目标缺失则依据 journal 的 `previous_hashes` 回退到最近完整阶段；目标存在但 hash 不匹配、journal 缺失或 revision 无法判断时，写入 `needs_reconcile`，禁止自动覆盖和自动重放。恢复完成后 journal 变为 `committed`、`quarantined` 或 `needs_reconcile`。

## 状态、revision 与 lease

`batch.status`：

```text
planned → scanning → ready → running
running → paused | cancelled | completed | failed
paused → running | cancelled
failed → running（仅人工确认）
scanning → failed | cancelled
```

`item.status`：

```text
queued → extracting → facts_review → metadata_pending → metadata_running → ready_to_create → created
facts 阶段：queued | retryable facts failure → extracting → facts_review
metadata 阶段：accepted facts → metadata_pending → metadata_running → ready_to_create
Provider 失败：extracting → failed 或 unknown_result（仅该 item）；metadata_running → failed 或 unknown_result（仅该 item）
facts 失败回边：retry-item(stage=facts, manual_confirmation=true) → queued；metadata 失败回边：retry-item(stage=metadata, manual_confirmation=true) → metadata_pending；不得使用无 stage 的 failed/unknown_result → queued 通用回边
任一阶段的 skipped 都只能由人工确认产生，不能隐式重试
```

`recovery_status` 独立于业务状态，值为 `clean`、`needs_reconcile`、`quarantined`。任何 `needs_reconcile` 项目禁止进入 Provider 调度或档案创建。

每次 item 状态/结果变化递增 `item.revision`；每次批量状态或 item 摘要变化递增 `manifest.revision` 和全局 `commit_seq`。写入必须带 `expected_revision`，不匹配返回 `WB-BATCH-REVISION-409`。

每次 Provider 调用生成一个 lease：`attempt_id`、`fence_token`、`owner_instance_id`、`issued_at`、`expires_at`、`deadline_at`。`attempt_count` 是总调用次数，首次调用计 1，最多 3 次；fallback 参数请求属于同一 attempt 的内部 exchange。服务端只接受 item revision、attempt_id 和 fence_token 全部匹配的返回；晚到结果记录 `stale_result`，不覆盖当前结果。

取消、超时、进程退出、连接中断等无法判断 Provider 是否已处理时进入 `unknown_result`，不自动重放；必须人工确认后新建 attempt。

## 跨进程幂等与档案关联

`idempotency_key` 是以下 canonical JSON 的 SHA-256，不使用字符串拼接：

```json
{"accepted_fact_set_hash":"...","authoring_schema_revision":"...","batch_id":"...","item_id":"...","metadata_selection_hash":"..."}
```

工作区使用 `FileMode.CreateNew` 创建 `idempotency/<key>.json` reservation。reservation 状态：`reserved`、`document_committed`、`linked`、`failed`、`needs_reconcile`；字段包括 owner_instance_id、fence_token、document_id、document_path、canonical_input_hash、created_at、updated_at。

重复请求读取同一 reservation 并返回同一 document 映射；canonical input 不同返回 `WB-BATCH-IDEMPOTENCY-409`。崩溃恢复规则固定：reservation 存在但档案不存在则校验 lease 后继续或标记 `needs_reconcile`；档案存在但 sidecar 不存在则禁止重建覆盖，进入 `needs_reconcile`；reservation 损坏则 quarantine 并拒绝创建。

document_id 从 idempotency key 稳定生成，不能依赖随机 draft ID。正式档案和 evidence sidecar 通过 journal 关联，只有二者都提交成功后 item 才能成为 `created`。

## 证据 sidecar

`evidence/v1/<evidence_id>.json` 使用契约文件中的 machine-readable schema。locator 使用：`source_unit_id`、`start_utf16`、`end_utf16`、`line_start`、`line_end`、`heading_path`；offset 为 normalized snapshot 的 UTF-16 code unit，0 起始，end exclusive，行号 1 起始。

服务端读取自己的 normalized snapshot，验证 `text[start_utf16..end_utf16] == quote`，并验证 source snapshot hash、locator hash、quote hash。失败分别返回 `WB-BATCH-EVIDENCE-SOURCE`、`WB-BATCH-EVIDENCE-LOCATOR`、`WB-BATCH-EVIDENCE-QUOTE`。Provider 返回的 evidence 只是候选，服务端重新定位并生成权威 sidecar；客户端不能提交或替换 sidecar。

## Provider 调度与错误

批量调度器按整个 Studio 进程和 Provider 类型限制并发：local=1，cloud 默认=2，配置最大=4。每个 logical attempt deadline 为 120 秒，覆盖握手、请求、内部 fallback、响应读取和退避等待；已有 Provider 的 HTTP timeout 只能取更短的剩余 deadline。

`BatchAttemptResult` 必须记录 attempt_id、provider_request_id、provider_fingerprint、started_at、finished_at、latency_ms、token_usage、outcome、error_class、retry_after_seconds、correlation_id 和 result_ref。`Retry-After` 支持整数秒和 HTTP-date，规范化到 0–300 秒。

408、429、502、503、504 为瞬态错误；最多 3 次 logical attempt，遵守 Retry-After、指数退避和抖动。认证、格式、策略和参数错误为永久失败。timeout/cancel/network unknown 为 `unknown_result`，不自动重放。日志不得记录 API Key 或完整资料原文。

## Cache

cache 位于 workspace 根的 `cache/v1/`，只保存可跨 batch 复用的 provider payload；cache key 只使用 normalized 内容，不使用 batch-local `source_snapshot_id`；raw hash 只用于审计。字段固定为：`normalized_content_hash`、`unit_hash`、`normalization_revision`、`splitter_revision`、`stage`、`prompt_revision`、`provider_id`、`provider_fingerprint`、`model_parameters`、`output_schema_revision`、`registry_snapshot_hash`、`accepted_fact_set_hash`、`metadata_selection_hash`、`identity_selection_hash`。

V1 的 `stage` 只允许 `facts` 或 `metadata`；未使用的阶段字段统一使用 `empty_set_hash`：facts 阶段的 `accepted_fact_set_hash`、`metadata_selection_hash`、`identity_selection_hash` 都为空；metadata 阶段复用已审核事实集和元数据选择哈希，`identity_selection_hash` 为空。所有 hash 字段命名统一为 `*_hash`。cache 由 entry、payload 和 commit marker 三个文件组成；按 payload flush/rename → entry flush/rename → marker prepared flush/rename → marker published 原子替换提交，任一文件缺失、哈希不匹配、marker 未 published 或 orphan 都整体进入 quarantine。cache payload 使用严格的 facts/metadata 子 schema，禁止保存 batch/item/source/evidence/document 引用。cache 命中时服务端按 `cache_fact_fingerprint` 为每个事实生成当前 batch 专属 `fact_id`，并在 materialization sidecar 保存映射；只写入结构校验成功的正结果，取消、超时、429、5xx 和 unknown_result 不写负缓存。损坏、版本不匹配或 key payload 不一致的 entry 移入 quarantine，不复用。

## Web API、consent 与 ownership

V1 路由和载荷 schema 固定在契约文件：`scan`、`create`、`get`、`consent`、`start`、`pause`、`cancel`、`claim`、`retry-item`、`review`、`item-detail`、`source-unit`、`prebatch-source-unit`、`create-documents`、`report`。

批量 owner 使用工作区 marker 中的持久 `owner_id`；session_id 只用于当前 CSRF/Origin 会话。Web 进程重启后，同一 workspace marker 的新 session 可以重新认领自己的批量任务；不同 owner 或不同 workspace 不能读取或控制。所有读写批量路由都要求 loopback、合法 Origin、CSRF 和 owner 校验。

`consent` 路由先签发一次性 token；专用响应只返回一次原始 token，持久文件只保存 `token_hash`。请求同时提交 `authorized_item_ids`，它是该 token 的最大 item 授权范围；`start` 每次提交非空 `target_item_ids`，只能是授权范围的子集，并把目标集合写入 start journal。token 绑定 `batch_id`、authorized item 集合、`{snapshot_id,snapshot_hash}` 对集合、Provider fingerprint、模型参数、发送范围、consent hash、owner_id、workspace marker hash 和 claim_generation。`start` 用 consent revision + state=issued 做原子 CAS；facts-only token 在 facts 目标集合排程提交后消费，facts-and-metadata token 在 facts 排程后保持 issued、在 metadata 目标集合全量进入 running 且排程 journal 提交后、Provider 调用前消费；Provider 的部分失败按 item 记录，失败项必须重新签发覆盖其 retry 子集的 consent。`start` 只消费仍有效且 owner marker 匹配的 token。所有写操作携带 `expected_revision`；版本不匹配返回 409，不静默覆盖。

V1 UI 只显示中文状态、风险和建议，不暴露 manifest 内部字段。批量流程硬拒绝 `expressions` stage，即使客户端直接提交也返回 `WB-BATCH-STAGE-422`。现有单份流程继续支持 expressions，不受批量边界影响。


`create` 时一并选择 Provider 和模型参数；服务端解析实际 Provider 后生成稳定 fingerprint，使 manifest 从创建起就是可恢复的完整状态。客户端不得提交 fingerprint。分配 batch 前先持久化 create reservation，分配 batch 与 promotion operation 后再 CAS 更新为 `promoting`；同一 create idempotency key 搭配不同 scan/provider/model/pipeline 载荷必须返回幂等冲突，同 key 未完成时返回可重试的 in-flight 冲突。`consent` 只确认同一 Provider、snapshot 和 authorized item scope。batch-level `start` 不允许隐式选择全部 item，也不允许目标 item CAS 半成功：目标集合中任一 item 不符合阶段条件时整次排程失败，不改变其它 item。item 明确保存 `facts_result_ref`、`metadata_result_ref` 和 `result_stage`，禁止用一个模糊的 `result_ref` 推断阶段。`metadata_pending` 只表示等待 metadata attempt，`metadata_running` 才表示 Provider attempt 已经占用 item。
## Revision 13 基础闭环规则

- **API 可执行性：** 每条路由必须引用 request、response 和 error schema；错误响应带 HTTP 状态、`correlation_id`、重试语义；CAS 冲突必须返回 expected/actual revision。批量建档返回逐项结果，不把单个 reservation 冒充批量响应。
- **资料切分与证据：** snapshot 先规范化为 UTF-8/NFC/LF，再生成带位置和 hash 的 source unit。事实对象必须带 `fact_id`、客观陈述、类型、确定程度、source unit 和至少一个 evidence ID；服务端验证 fact → evidence → snapshot。
- **证据哈希：** `locator_hash`、`quote_hash`、`binding_hash` 使用契约中的固定 CanonicalJson/NFC/LF 公式；客户端提供的 quote、locator 或 hash 不得直接信任。
- **执行审计：** 每个 attempt 绑定 batch、item、provider、owner instance、lease、fence token、deadline 和 provider request ID；晚到结果只有在 fence、attempt 和 item revision 全匹配时才可提交。
- **重启认领：** workspace marker 的 `owner_id` 是持久所有权；session 只负责 CSRF/Origin。重启后新进程通过 `claim` 路由和 CAS 认领，只有不存在未过期 lease 时才允许，且递增 `claim_generation`。
- **共享缓存：** cache 位于 workspace `cache/v1`，只保存可验证的 provider payload，不保存可跨 batch 复用的 evidence 引用；命中后重新生成当前 batch 的 result/evidence sidecar。
- **边界：** V1 只接收 `facts`、`metadata`；客户端提交 `expressions`、身份权限、人物/家族/地点绑定一律返回 `WB-BATCH-STAGE-422`。

## V1 验收

- machine-readable contracts JSON 可解析；50+ item 任务可建立、保存、恢复和报告；每个崩溃窗口有确定恢复结果；
- `batches/` 不进入作者列表、快照、校验、编译和导出；上传路径、碰撞、大小和编码边界均被拒绝或清晰提示；
- 状态转移、revision、lease fencing、unknown_result、人工重试和晚到响应有专项测试；
- quote/source/locator 三重绑定、客户端伪造拒绝、sidecar/档案关联可验证；
- local=1、cloud=2（最大 4）、120 秒 deadline、Retry-After 和 3 次总 attempt 可观察；
- cache golden vectors、逐字段失效、损坏 entry 隔离和 V1 expressions 硬拒绝通过；
- Web scan→create→consent→start→review→item-detail/source-unit/prebatch-source-unit→create-documents→report 全链路可达，含预批次 promotion、重启后的 claim、cache-hit materialization；现有单份测试、Worker/Provider Smoke、Launcher 和 release-check 全部回归通过；
- 人工确认前不创建正式档案，创建后保持 `needs_review`；不启动 Bannerlord、不同步游戏目录。

## 后续版本

- V1.1：按选定身份批量生成 expressions、表达权限审核、人工合并/拆分和互相引用辅助。
- V2：跨资料相似检测、冲突提示和人工确认后的人物/家族绑定。

## Gate

本文、计划和 Revision 13 契约 JSON 通过新的独立只读审查并返回 `VERDICT: APPROVED` 后，才允许建立代码写入租约；当前主模型不可用时的 `REVIEW_ERROR` 或本地 Worker 结果不构成批准。

## Revision 13 追加规则

- scan/snapshot/source unit 使用 `prebatches/<scan_id>/`，create 通过原子 promotion 进入正式 batch；目录契约与 schema path 必须一致。
- `scan_id` 是永久来源 provenance；预批次和正式 batch 都保留它，正式 batch 额外绑定 `batch_id`。`scope_kind` 只决定容器路径，不再与 `scan_id` 冲突；prebatch 必须绑定 scan、禁止 batch，batch 必须绑定 batch、禁止 prebatch，`scope_id`、文本路径和实际目录必须互相一致。
- create 请求必须提供 Provider 选择但不接受客户端 `provider_fingerprint`；服务端按实际解析到的 Provider 生成 fingerprint，并在 batch 分配前持久化 create reservation。相同 key 与 canonical input 返回同一 batch，不同载荷明确返回幂等冲突。
- promotion 必须有独立 journal、固定 operation_id；promotion journal 的 `state` 是唯一恢复权威，`scan.promotion.state` 只是投影。恢复时先读 journal、校验源与目标哈希，再完成或隔离目标，提交 journal 状态后最后更新投影。
- facts 与 metadata 结果分别绑定，attempt 必须带 `stage`、`result_schema_id`、`result_ref` 和 `item_revision`；两种 result 都记录 `created_from_item_revision`，并由服务端双向校验 attempt/result/item 的 revision、stage 和引用，facts/metadata 结果不可混用。
- claim_generation 进入 manifest、consent、start 和 attempt；claim/rebind 会使旧 generation 的未消费 consent 失效，阻断旧进程继续提交。consent 消费使用 revision + state=issued 的 compare-and-set；并发 start 只有一个成功，失败不消耗 token。
- `token_usage_known` 与嵌套 `token_usage.known` 必须一致；known=false 时 token 数值为 null，known=true 时不得为 null。unknown_result 使用明确占位 provider request ID，不把缺失 Provider 数据伪装成真实计量。
- Provider fingerprint、owner instance 和 workspace marker 均由服务端生成或校验；客户端不能伪造运行实例。
- cache entry、payload、commit marker 三者 hash/schema 必须一致；payload 只能保存 facts/metadata provider 结果，不得保存 batch 私有引用；缓存命中生成 `attempt_kind=cache_hit` 和 `cache_materialization` sidecar；空事实结果不生成证据物化；共享 cache payload 只能位于 `cache/v1/`。
- Provider fingerprint 使用固定的 `awake.worldbook.provider-fingerprint.v1` 公式生成，不包含 API key、worker secret 或 authorization header；解析失败在 create reservation 前返回 `WB-BATCH-PROVIDER-422`。
- API 对外所有读取和写入响应都使用 public projection：`public_scan`、`public_manifest`、`public_item`、`public_review_projection`、`public_source_unit`、`public_review_decision`；不返回 workspace marker、owner_id、owner instance、provider fingerprint、lease/fence 或内部路径。
- journal 明确记录 `start`、`claim_rebind`、`pause`、`cancel`、`retry_item` 和 `create_reservation` 的 CAS/consent/claim 字段；跨文件恢复不得依赖目录猜测。
- cache 提交固定为 payload flush/rename → entry flush/rename → marker prepared flush/rename → marker published 原子替换；只有 published marker 才可命中，任何缺失/半提交/哈希不一致都整体隔离。
- cache facts/metadata payload 各自有严格 schema，facts payload 只携带事实文本和待重新绑定的证据候选，metadata payload 只携带标题、摘要和分类候选。
- `create-documents` 强制 `create_mode=needs_review`；持久 consent 与浏览器响应分离，浏览器不接收 token_hash、workspace marker 等内部字段。
- Batch domain/kind 在建档前通过 authoring projection 映射：`military → war`，`definition → state`，`chronology/geography → fact`，然后再经过现有作者 schema 和 builder 校验。


## Revision 13 闭合修订

- **创建幂等与 promotion：** create reservation 现在有服务端 opaque `reservation_id`、稳定 `allocation_id`、owner fence、`claim_generation` 和 `claim_expires_at`。reservation 先以 `allocation_pending` 持久化，再执行可重放的 batch 分配；即使在 batch_id 写入和 prepared journal 之间崩溃，恢复也只能复用同一 allocation_id，绝不分配第二个 batch。promotion journal 提交后必须执行 `promoting → committed` CAS，再更新 scan projection。
- **失败状态：** committed 重试返回已持久化的 public manifest；failed 返回不可重试的 `WB-BATCH-PROMOTION-409`；needs_reconcile 返回可重试的 `WB-BATCH-PROMOTION-RECONCILE-409`。两种终态都永久保留 batch_id。
- **两阶段结果：** `result_stage` 代表最近一次已提交结果，`active_attempt_stage` 代表正在运行的 attempt。`metadata_pending` 保持 `active_attempt_stage=none`，`start(stage=metadata)` 先 CAS 到 `metadata_running` 并设置 active stage，只有后者才允许 Provider 调用；两者在 metadata 提交前都保持 `result_stage=facts`。
- **事实集绑定：** metadata attempt、metadata result、cache materialization 和 create-documents CAS 都必须重新计算当前 accepted fact set hash；旧事实集不得生成或提交 metadata。
- **公开投影：** public item、create-documents result、report 不再返回内部 `document_path`、任意异常文本或 workspace 元数据；report 改用封闭字段的 public projection，错误只返回公开错误码和用户可读摘要；item-detail 同时公开 metadata 候选，供编辑者选择；inflight error 只返回 opaque `reservation_id`，details 使用白名单字段；prebatch public source unit 明确禁止 `batch_id`。
- **审核到 metadata：** review `accepted` 只在当前 `result_stage=facts`、事实集非空且服务端重算 hash 成功时提交；`facts_and_metadata` consent 的 authorized item scope 才能将目标 item CAS 到 `metadata_pending`（active stage 为 none），`start(stage=metadata,target_item_ids=...)` 再以全量原子 CAS 将其转为 `metadata_running`（active stage 为 metadata）后才调用 Provider；metadata 排程提交时消费该 consent（全量 target CAS 与 schedule journal commit 后、Provider 前），Provider 之后的部分成功/失败按 item 结算，失败子集必须重新授权；`facts` consent 只完成事实阶段并留下“等待 metadata 授权”的可见状态。
- **promotion 接管与终态：** promoting 期间 owner fence 过期必须走 claim-rebind CAS，新 fence/new claim_generation 生效后旧 owner 永久失效；journal 的 aborted/quarantined/needs_reconcile 分别映射到 failed 或 needs_reconcile reservation、HTTP 状态、错误码和重试规则，并永久保留 batch_id。
- **路径与缓存恢复：** schema-specific 路径必须通过统一段级校验；cache recovery 单独覆盖 marker-only、published 缺 entry/payload、quarantined、needs_reconcile 与阶段不一致。cache-hit fact_id 改为固定长度的 server-owned 哈希 ID。


## Revision 13 门禁补充

- promotion 在 `reserved → allocation_pending → promoting → committed` 之间保留稳定 `allocation_id`；`allocation_pending` 可以暂时没有 `batch_id`，但一旦分配记录落盘，后续所有恢复和重试必须复用该 `batch_id`。
- promoting 状态的接管只能在 claim 过期后执行 reservation/journal 双写 CAS；旧 fence、旧 claim_generation 的提交统一返回 `WB-BATCH-CLAIM-409`。
- item-detail 的 public projection 必须包含只读 metadata 候选；report 必须使用 `batch-report-public.v2`，不得直接暴露内部 report、last_error 或任意 report_summary object。
- facts 审核与 metadata 调度是两个明确阶段：`accepted + facts_and_metadata consent.authorized_item_ids → metadata_pending(active=none) → start(stage=metadata,target_item_ids=subset) → metadata_running(active=metadata)`；目标集合必须全量通过 eligibility/CAS，否则整次排程不改变任何 item。metadata 排程提交时消费 consent；之后每个 item 独立进入 ready/failed/unknown_result，失败子集重新签发 consent 后，只有 `retry-item(stage=metadata)` 能回到 `metadata_pending`，随后再次 `start(stage=metadata)`，不伪造结果。
- Revision 13 的 P2 测试/发布接线（BatchTests、统一路径 validator、release-check 调用 test.ps1）仍为获批后实现项；在代码门禁前只更新契约、设计和机械自检。

- metadata retry 的唯一回边是 `retry-item(stage=metadata, manual_confirmation=true)`：它只在最近 metadata attempt 失败或 unknown_result 时执行 CAS，不调用 Provider；它保留 `result_stage=facts`、accepted facts 和 hash。重新签发且覆盖 retry 子集的 facts_and_metadata consent 后，下一次 Provider 调用必须经过 `start(stage=metadata,target_item_ids=retry subset)`。

- consent 消费时点唯一规则：metadata 目标集合全量 CAS 到 `metadata_running` 并提交 start-schedule journal 后，先 CAS `consent.state=issued → consumed`，再调用 Provider；Provider result commit 不得再次消费 consent，排程部分失败则不改变 item 或 consent。

- journal 状态边界：通用跨文件 journal 与 promotion journal 是不同 schema；实现不得把 promotion 的 copying/verified/renamed 阶段写入通用 journal，也不得用通用 journal 替代 promotion 恢复锚点。
