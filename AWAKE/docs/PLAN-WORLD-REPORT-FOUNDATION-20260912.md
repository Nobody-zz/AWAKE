# Plan: 本周动态基础（P0）

> Status: `split_superseded_not_implementable`  
> Scope: AWAKE 模组本体；不包含世界书正文、角色卡、人物对话或互动事件。  
> Source evaluation baseline: working tree `AwakeVersion.BuildId=awake-20260911-g3b-persona-storage-001`, Git HEAD `f8cfc22c93dc28fc3fac69079e32c46dd9bef457`; this tree has pre-existing uncommitted changes and is not a candidate identity. Current verified evidence ceiling is E2.

## 1. Goal

建立一条独立、可存档、可恢复的“世界事实 → 本周动态”基础链路。世界书、人物对话和未来事件只能消费该链路的事实与报告，不得成为其写入前提。

本批次的可观察闭环为：

```text
持久化世界事实
  -> 完整周窗口查询
  -> 一次性生成正式报告快照
  -> 持久化报告状态
  -> 菜单显示“本周动态”
  -> （可选）世界知识投影
```

## 2. Locked product decisions

- 正式报告的玩家名称是“本周动态”；实时但未结算的内容称“近期动态”。
- 新正式报告按有内容才显示的栏目排列：`政治与外交`、`战争与领地`、`人物近况`、`地方情况`。
- P0 只支持前三类事实的报告结构；`地方情况`是预留栏目，不因凑版面生成文字。
- “玩家行动、外交、战役、随机调剂”是**事实来源**，不是报告栏目。
- P0 不接新的 Bannerlord 游戏事件回调；它只修复并固定报告基础。第一条真实来源另开 P1 纵向切片。
- 正式报告以完整的七天窗口结算；季度汇总、AI 文案和互动事件全部后置。

## 3. Confirmed facts and boundary

- `WorldEventServices.EnsureKnowledgeReadyAsync` 目前将周报生成置于 `CanProject(...)` 之后；`AwakeRuntime.EnsureKnowledgeReadyIfNativeReadyAsync` 和 `ProbeExtension` 的读档恢复也先要求 Native Knowledge Ready。因此 Native Knowledge 未就绪时，周报不会生成。
- 该方法从 `WorldEventLedger.CaptureSnapshot()` 的内存快照取事实，而账本缓存容量有限；正式报告不能以此作为完整周数据的唯一来源。
- `WeeklyReportService.BuildText(...)` 当前从滚动的近七天构建“世界周报”；`AwakeTerminalBehavior` 使用该路径，未读取已保存的正式 `weeklyReports` 快照。
- `WeeklyReportService` 当前输出 `politics/economy/culture/war` 四个固定栏目；`WeeklyReportBrowserVM` 与中英文语言文件仍显示“世界周报”。既有存档和既有 v1 报告必须继续可读。
- 当前日志曾记录 `world_state_storage_permission_denied`。P0 必须让这一失败清晰可见，不能以 Persona `SyncData` 悄悄替代世界状态存储。

## 4. Target ownership and state transition

### 4.1 Ownership

| Owner | Owns | Must not own |
| --- | --- | --- |
| `WorldStateStore` | 世界事实与报告快照的持久化、窗口查询、状态写入 | 世界书可用性、UI 文案决策 |
| `WorldEventLedger` | 去重、短期缓存、存档事实的加载与写入协调 | 以容量受限快照代替持久化窗口查询 |
| `WeeklyReportService` | 纯确定性报告构建、栏目投影、文本渲染 | 读取游戏实时对象、直接写存档 |
| `WorldEventServices` | 按顺序编排账本、报告、可选投影 | 把 Native Knowledge 当作报告生成前置条件 |
| `AwakeTerminalBehavior` | 展示正式报告或明确标识的预览 | 生成或篡改报告快照 |
| `WorldKnowledgeProjectionService` | 消费已持久化事实/报告并可重试投影 | 决定报告是否完成 |

### 4.2 Runtime sequence

1. 战役调度获取当前游戏日和当前 campaign generation。
2. `ProbeExtension` 在 CampaignSessionReady/读档恢复中先确保 Storage 就绪，然后无 Native Knowledge 前置地调用 `EnsureFormalReportsReadyAsync`；小时调度也独立调用该方法，且不受 `EnableEventEngine`、弹窗或 Messenger 门控影响。
3. 服务确认 `WorldStateStore` 对当前战役可用；若不可用，记录一次带原因的延迟状态并安全退出。
4. 账本从存储加载；服务按每一个已结束的七天窗口向存储查询完整事实集合。
5. 对每个窗口：如果已有合法、完成且内容指纹一致的固定快照，直接复用；否则由 `WeeklyReportService` 确定性构建并按单写者契约写入。
6. 菜单读取最近一份完成快照；没有完成快照时才构建只读“近期动态”预览，并清楚标识。
7. `EnsureKnowledgeReadyIfNativeReadyAsync` 只在 Native Knowledge 可用时投影已保存的事实和报告；不可用时保留待投影状态，后续重试。

任何后台工作只带标量快照、campaign generation、cancellation token 与 correlation ID；不得保存或跨线程访问 TaleWorlds 实时对象。

## 5. Compatibility contract

### 5.1 Facts

- 保持 v1 `WorldEventRecord` 现有字段可读、可写。
- 新分类只以可选字段或 v2 投影出现，不清理、不重写旧记录。
- 旧 `politics` 映射到新展示栏目“政治与外交”，旧 `war` 映射到“战争与领地”；旧 `economy` 与 `culture` 在新展示中归入“地方情况”。
- 新的 `people` 与 `local` 只在对应契约、验证器和投影均支持后写入；P0 本身不制造任何新事件事实。

### 5.2 Reports

- 保留 `awake.worldbook.weekly-report.v1` 的读取和投影支持，既有快照不改写。
- 新生成的报告使用 `awake.worldbook.weekly-report.v2` 报告契约，栏目为 `politics/war/people/local`，只输出非空栏目。
- v2 schema、正例/反例 fixture 与 C# validator dispatch 同时交付；v1 与 v2 均须能被 UI 和投影器读取。v2 将 `policyVersion`、规范化 `contentFingerprint`、窗口边界和来源事实 ID 定为必选字段。
- 同一报告 ID 只能接受同一份有效、指纹一致的快照：同内容重试返回幂等成功；不同内容返回稳定冲突错误并保留原快照。
- UI 可以以当前中文名称渲染 v1/v2 报告，但不得修改已保存的 v1 文本或来源闭包；无法识别的版本显示安全的不可用状态，投影器只跳过该报告并留日志。

### 5.3 Failure and rollback

- 存储不可用：不生成报告、不写替代状态；UI 显示报告暂不可用，日志含稳定错误码与 campaign generation。
- 报告写入失败或结果不确定：不宣布完成；重读该 reportId 并仅在窗口、来源闭包和内容指纹完全一致时确认成功，否则保留重试条件，不重复追加事实。
- 已保存但校验失败的报告：只修复该报告快照；原始事实不删除。
- Native Knowledge 不可用：报告成功仍有效；投影延后。
- 本批次不做不可逆存档迁移，因此回滚代码后，v1 读取路径仍可读取旧状态；v2 报告须有安全的只读降级提示，而非崩溃。

## 6. Exact implementation slices

### Slice A — storage query, retention and report-state seam

Files expected: `WorldStateStore.cs`, `WorldEventLedger.cs`, `WorldEventContracts.cs`.

1. 增加按 `[startDay, endDay]` 读取持久化事实的受限接口；结果按稳定 ID、发生时间排序并显式返回成功、空窗口或读取失败。
2. 保持内存账本为缓存与去重辅助，不再作为正式报告唯一输入。
3. P0 不按“200 条”这样的原始条数截断持久化事实。为证明完整周窗口，原始事实至少保留到其所属报告已稳定写入；长期压缩/清理另开批次，在 P0 中只记录存储大小诊断，不删除事实。
4. 规定报告状态的写入边界：同一 AWAKE 战役会话仅由现有 `WorldStateStore` 命令队列写入该逻辑键；先核实宿主是否提供 CAS。若无 CAS，P0 明确不支持多进程/外部写者竞争，并在 commit-unknown 后重读、比对窗口、来源闭包和指纹，任何不一致均 fail closed。
5. 将“存储可用/不可用”“窗口已完成/待写入/已复用/冲突”做成明确结果，而非用空集合掩盖错误。
6. 为每一次写入/复用记录结构化日志：BuildId、event name、reportId、窗口、campaign generation、结果、错误码和 correlation ID。

Validation: 假存储/离线测试覆盖完整窗口、排序、重复键、存储失败、>50、>200、空窗口、同进程并发事实写入、commit-unknown 重读与报告冲突。

### Slice B — formal report v2, schema and v1 reader

Files expected: `WeeklyReportService.cs`, `WorldEventContracts.cs`, `WorldKnowledgeProjectionService.cs`, focused tests/SDK smoke.

1. 在 `tools/worldbook-contract/v2/` 新增 v2 schema 和 v1/v2 正例、反例 fixture；C# validator 以 `schemaVersion` 分派，不能以“当前单一 validator”暗中接受 v2。
2. 将报告构建明确分为 `BuildFormalWindow(...)` 与 `BuildPreview(...)`；预览不得返回可持久化的正式报告 ID。
3. 实现 v1 分类到 v2 显示栏目的纯投影；不修改旧事件。
4. 新 v2 报告只创建有事实的栏目；标题使用当前锁定的朴素中文用词。
5. 为来源 ID、窗口和规范化内容计算稳定指纹，并将其持久化在报告状态和 v2 报告中；发现同一 `reportId` 的不一致状态时 fail closed 并记录诊断。
6. 投影器同时接受 v1/v2 报告；不能识别的报告只跳过该报告并留日志，不中断事实投影。

Validation: schema parser、v1/v2 fixture、v1 读取投影、v2 生成投影、确定性输出、空栏目不输出、来源闭包不丢失、重复构建语义等价、同 ID 异内容冲突。

### Slice C — orchestration decoupling and lifecycle reachability

Files expected: `WorldEventContracts.cs`, `AwakeRuntime.cs`, `AwakeEventBehavior.cs`, `ProbeExtension.cs`.

1. 新增 storage-only `EnsureFormalReportsReadyAsync`；将当前 `EnsureKnowledgeReadyAsync` 拆成“确保正式报告就绪”和“尝试知识投影”两个单向阶段，并保留兼容门面供旧 caller 使用。
2. `ProbeExtension.RestoreCampaignStateAsync` 在 Storage ready 后、Native Knowledge 检查前调用正式报告阶段；读档恢复不能再依赖 Native readiness 成功。
3. 在 `AwakeEventBehavior` 将报告刷新从 `EnableEventEngine`、弹窗与 Messenger 门控中移出；事件引擎自身仍保留原门控。
4. 先完成报告持久化，再检查 Native Knowledge。
5. 使用已有 campaign boundary 与 generation 检查；在每个异步 await 后再次确认当前会话。
6. 限制同一时间只有一个报告补齐任务；取消、读档切换和会话结束不得留下旧战役的写入。

Validation: CampaignSessionReady、读档恢复、Native Knowledge false、`EnableEventEngine=false`、读档 generation 变化、取消、重复调度与失败重试。

### Slice D — menu rendering

Files expected: `AwakeTerminalBehavior.cs`, `WeeklyReportBrowserVM.cs`, `ModuleData/Languages/CNs/awake_strings-zh-HANS.xml`, matching English language file and focused test seams.

1. 菜单优先读取持久化的最近一份合法完成“本周动态”，不再从 ledger 缓存冒充正式报告。
2. 没有正式报告时显示“近期动态（未结算）”；这一路径只读，不写 `weeklyReports`。
3. 存储或报告不可用时显示简短、非技术性说明；详细错误仅写日志，且不得回退为旧缓存报告。
4. `WeeklyReportBrowserVM` 以输入状态区别正式、预览与不可用；中英文 XML 及所有调用点同步为锁定的朴素用词。
5. P0 无玩家可调阈值、频率或行为，因此不加 MCM 项；该判断在实现审查中复核。

Validation: 正式报告、预览、空数据、存储不可用、v1/v2 显示的菜单文本、BrowserVM 状态和终端入口调用链。

### Slice E — candidate evidence

1. 运行聚焦存储/报告测试、v1/v2 schema fixture 校验及日志字段断言。
2. 运行 `tools\\build.ps1`、`Awake.SdkSmoke.exe`、项目要求的 `maf-lint.ps1`。
3. 生成新 BuildId、源码 DLL SHA-256、E2 证据和 checkpoint；不得用现有 010 或历史同步证据冒充新候选。
4. 只有用户明确授权、Bannerlord 已退出后，才同步并开展 E3；E4/E5 由匹配新 BuildId 的用户实机日志证明。

## 7. Acceptance matrix

| Case | Entry -> observable result | Minimum evidence |
| --- | --- | --- |
| Complete window | 已结束周被调度后，从持久化事实生成一个固定报告 | focused test + E2 log/assertion |
| Idempotency | 同一窗口重复调度、读档或重试，不产生第二份报告或重复事实 | focused persistence test |
| Content conflict | 同一 reportId 收到不同窗口/来源/指纹的合法内容时保留原快照并返回稳定冲突码 | focused persistence test |
| Cache boundary | 一周事实数超过内存缓存容量，报告来源清单仍完整 | focused test |
| Retention boundary | 超过 200 条事实时，未完成周窗口仍不丢失；不把读取失败解释为空窗口 | focused persistence test |
| Knowledge unavailable | Native Knowledge false 时，报告照样完成；投影延后 | focused test + structured result |
| Storage unavailable | 权限/存储失败时不写伪报告，UI 安全降级 | focused failure test |
| Compatibility | v1 事实和 v1 报告可读取、显示与投影 | fixture test |
| Preview isolation | “近期动态”不写正式报告状态，也不覆盖固定快照 | focused UI/service test |
| Observability | 生成、复用、冲突、存储失败与延迟投影日志均包含已定义的字段 | focused smoke log assertion |
| Save/load | 新候选下存档、退出、读档后报告 ID/来源/文字稳定 | E5 user-run evidence |

## 8. Explicit non-goals

- 不新增外交、战役、人物或地方的 Bannerlord 事件观察器。
- 不修改 `AwakeEventEngine`、事件 JSON 或互动弹窗。
- 不接人物角色卡、NPC 对话、Persona Storage、世界书正文或 AI 摘要。
- 不添加季度报告、公告、政令、经济模拟或随机社会事件。
- 不同步游戏目录、启动游戏、改版号或发布。

## 9. Risks requiring review attention

1. `world_state_storage_permission_denied` 的真实宿主权限根因尚未修复；P0 只能保证正确降级和诊断，不能宣称 E4/E5 可达。
2. v2 周报契约会跨存档、投影器和 UI；审查须核对 v1 reader、v2 schema 与 dispatch 是否真正可达。
3. 存储查询的线程/生命周期必须与 campaign generation 严格绑定，防止读档后旧任务写入新战役。
4. 宿主若没有 CAS，跨进程/外部写者并发不属于 P0 支持范围；实现必须证明单写者队列、commit-unknown 重读确认和 fail-closed 行为。
5. “人物近况”虽进入契约，但没有真实数据来源前不得输出造作内容。

## 10. Gate and next action

`plan_status = revised_after_review_round_3`  
`review_status = revision_required; review_cap_exhausted`  
`user_signoff_required = yes, after VERDICT: APPROVED`  
`primary_executor = current AWAKE mod task`  
`minimum_evidence = E2 offline before any candidate; E5 for save/load completion`  

Superseded by user-directed split on 2026-09-12: storage concerns now belong to `PLAN-WORLD-WINDOW-STORAGE-20260912.md`; report/UI concerns belong to `PLAN-WEEKLY-DYNAMICS-20260912.md`. This combined plan is retained only as trace evidence and cannot be implemented.

## 11. Round 2 lock changes (supersedes conflicting earlier wording)

### 11.1 Native-independent entry is a separate lifecycle task

The P0 implementation must not merely reorder `RestoreCampaignStateAsync`. At `CampaignSessionReady`, `ProbeExtension` starts a separate background `RestoreCampaignStorageAndReportsAsync(sessionGeneration)` immediately, independent of the Native readiness continuation. Its only responsibilities are Storage readiness, ledger/report-state load, and `EnsureFormalReportsReadyAsync`.

The existing Native-ready continuation may start a separate Native-dependent restore/projection task only after readiness succeeds. `AwakeRuntime.EnsureKnowledgeReadyIfNativeReadyAsync` remains Native-gated and must not be used as the formal-report caller. Tests must prove the storage/report task is scheduled and writes a report when Native readiness is false.

### 11.2 Report schemas, identifiers and fingerprint are exact

- Repair the v1 JSON schema to declare the already supported `visibility` object, and add a fixture produced by the existing v1 runtime. The fixture is byte-preserved; repairing the schema must not rewrite stored v1 reports.
- Add `tools/worldbook-contract/v2/weekly-report.schema.json`, plus valid and invalid fixtures. v2 requires `policyVersion`, `contentFingerprint`, `visibility`, source IDs, window extension fields and only nonempty sections.
- The C# validator dispatches strictly on `schemaVersion`; `WorldKnowledgeProjectionService` and UI accept both v1 and v2 through that dispatch.
- v1 keeps its existing ID `awake:report:weekly-{endDay}`. v2 uses `awake:report:weekly-v2-{endDay}`. A v1 snapshot is never overwritten or relabelled; UI prefers a valid v2 report for its window and otherwise displays its valid v1 report.
- `contentFingerprint` is uppercase SHA-256 of UTF-8 canonical JSON that excludes `contentFingerprint` itself. Canonical JSON recursively sorts object property names ordinally, preserves the ordered sections/items generated by the service, and ordinally sorts sets (`sourceEventIds`, identity IDs and an item's source IDs). Its input includes exactly: schemaVersion, reportId, period, generatedBy, policyVersion, sourceEventIds, sections, visibility, window start/end. Fixture tests pin expected hashes.

### 11.3 Versioned, chunked fact storage replaces the impossible unlimited single value

The existing v1 single world-events key remains read-only compatible. P0 adds a v2 index key and deterministic weekly chunk keys in the same private namespace:

```text
index:  campaign.world_events.v2.index
chunk:  campaign.world_events.v2.week-{endDay}-{ordinal}
```

The index records schema version, known window end days, ordered chunk keys, report-state location and migration marker; it never carries the full fact collection. A chunk contains facts only for one completed/open weekly window. A chunk is sealed before its serialized UTF-8 payload reaches 384 KiB, leaving room below the host's 512 KiB single-value limit; the next ordinal is then created. The formal report query reads every indexed chunk for its window, performs stable ID/time ordering and returns a typed result: `missing`, `empty`, `success`, `corrupt`, or `unavailable`.

`missing` is allowed only for an absent v2 index/window with no corresponding v1 data. `empty` means a syntactically valid, existing window with zero facts. Existing but malformed state is `corrupt` and fails closed; it can never become an empty report. P0 removes the 200-record truncation only for reportable v2 chunks. Raw v1 state stays readable; lazy migration/import is append-only and must not delete or rewrite it. Long-term archive/compaction remains out of scope.

### 11.4 Write and repair semantics

- Every v2 index/chunk/report write remains on the current AWAKE command lane. Implementation must document whether the host offers CAS; when it does not, external or multi-process writers are unsupported and are not silently merged.
- On a write timeout/unknown result, reread the target index/chunk/report and confirm the exact intended normalized data and fingerprint. Only that confirmation is success; otherwise return retryable/failed without declaring the report applied.
- A valid incoming report with zero source IDs is a legal empty-week report. `incomingReport == null` alone means no repair payload. A corrupt applied snapshot may be repaired by a valid empty report for the same window/version; a valid applied snapshot may never be replaced by different content.
- Same ID plus same canonical fingerprint is idempotent; same ID plus a different fingerprint returns `awake.world_state.weekly_report.content_conflict` and preserves the old snapshot.

### 11.5 Expanded implementation and acceptance scope

Slice A additionally owns `AiTaskConstants.cs` and v2 index/chunk query fixtures. Slice C additionally owns the independent `ProbeExtension` lifecycle entry, not just its restore-function internals. Slice D must include `WeeklyReportBrowserVM.cs` and both Chinese/English language resources.

Before implementation sign-off, the executor must capture the exact source-tree BuildId, Git HEAD and relevant dirty-file hashes in the candidate checkpoint. A new candidate BuildId is selected only after implementation begins; no existing 010 or G3B E2 evidence may be reused for this batch.

Additional required focused cases are:

| Case | Required observation |
| --- | --- |
| Native false entry | CampaignSessionReady schedules storage/report recovery and writes/reuses a report while Native readiness remains false. |
| Existing v1 fixture | Existing v1 generated report validates against repaired JSON schema and C# validator without byte changes. |
| Corrupt vs empty | Malformed root/records/reports returns `corrupt`; only valid empty window returns `empty`. |
| Chunk boundary | Facts spanning several chunks under one week produce one complete report; no single stored value exceeds 512 KiB. |
| v1/v2 same window | Valid v1 remains immutable; valid v2 uses versioned ID and UI selects v2 without overwrite. |
| Empty repair | A corrupt applied snapshot for a genuinely empty week is repaired by a valid empty report. |
| Fingerprint | Pinned fixtures prove canonical field set, ordering and expected SHA-256; mismatch conflicts rather than overwrites. |

## 12. Round 3 lock changes (supersedes sections 11.2–11.4 where conflicting)

### 12.1 No global v2 index; deterministic per-window manifest is the sole commit point

The v2 global index is removed because it would itself outgrow the 512 KiB value limit. For a complete window ending on `endDay`, the only mutable commit key is:

```text
manifest: campaign.world_events.v2.week-{endDay}.manifest
fact:     campaign.world_events.v2.week-{endDay}.facts-{ordinal}
report:   campaign.world_events.v2.week-{endDay}.report-{ordinal}
```

The manifest contains its own schema version, window bounds, phase (`collecting`, `sealed`, `reported`), monotonic local revision, fixed maximum of 64 fact chunks and 16 report chunks, every chunk key plus SHA-256, the report ID/fingerprint and a deterministic write-intent ID. It contains no fact/report payload. The deterministic keys mean the current day can derive the active/recent manifests without listing or retaining a global historical index; a future history browser is outside P0.

Each payload chunk is sealed below 384 KiB. If a window exceeds either declared chunk maximum, P0 returns `awake.world_state.weekly_window_capacity_exceeded`, creates no final report and never drops facts or converts the failure to an empty report. This is a bounded, testable failure instead of an impossible “unlimited” storage promise.

### 12.2 Multi-key recovery protocol

The host offers only per-key `Get/Set/Delete`; it has no CAS or transaction. P0 therefore uses this exact recovery protocol under the existing single AWAKE command lane:

1. Build normalized fact/report payloads and their hashes without mutating persistent state.
2. Write or verify every deterministic fact/report chunk. Existing same-key/different-hash data is a conflict and fails closed.
3. Read back every referenced chunk and verify its SHA-256.
4. Write the compact manifest last. The manifest is the sole visibility/commit point.
5. On timeout or unknown result, read the manifest and all referenced chunks. Success requires the exact write-intent ID, revision, references and hashes; otherwise return retryable/failed.

A crash before the manifest leaves deterministic, unreachable chunks that a retry can verify and reuse; they are not visible to reports. A crash after it leaves a complete, hash-verifiable manifest. A manifest that references a missing or hash-mismatched chunk is `corrupt`, never `empty`; it is not repaired automatically. After phase `reported`, the manifest/report snapshot is immutable except that an invalid/missing report payload may be repaired by the same version/window only after all source chunk hashes still verify. No multi-process or external writer is supported.

### 12.3 Exact v2 contract and fingerprint input

`policyVersion` is the required string constant `awake.weekly-report.policy.v2`. V2 requires exactly these top-level fields:

```text
schemaVersion, reportId, period, generatedBy, policyVersion,
contentFingerprint, sourceEventIds, sections, visibility, extensions
```

`extensions` permits exactly `awake:windowStartDay` and `awake:windowEndDay`, both JSON integers, with `windowStartDay <= windowEndDay`; their period must map to the same inclusive campaign-day interval. `contentFingerprint` is an uppercase 64-character SHA-256 hex string.

The fingerprint canonical object excludes `contentFingerprint` and contains every other required top-level field in the order listed above. Nested object property order is ordinal; the generator's sections/items retain their produced order; set arrays (`sourceEventIds`, `visibility.identity_ids`, item source IDs) are ordinally sorted. The v2 schema and pinned valid/invalid fixtures must carry the exact expected canonical UTF-8 JSON and hash.

### 12.4 Background storage permission boundary

The CampaignSessionReady/background report path must not call `PermissionGate.EnsureAsync`. Before opening or reading world-state storage, it calls a dedicated background-only `EvaluateWorldStateStorageAccess(...)` seam using `PermissionGate.Evaluate`; denied, unavailable or indeterminate results produce a structured `storage_access_not_granted` result and a noninteractive UI fallback. Only an explicit player-initiated future action may call `EnsureAsync`; P0 adds no such action.

### 12.5 Round-3 acceptance additions

| Case | Required observation |
| --- | --- |
| Multi-key crash recovery | Interrupted before manifest is invisible and retry reuses/verifies deterministic chunks; interrupted after manifest reads complete data. |
| Manifest corruption | Missing/hash-mismatched referenced chunk yields `corrupt`, never an empty or regenerated report. |
| Capacity bound | A 65th fact chunk or 17th report chunk returns `weekly_window_capacity_exceeded`; no data is silently discarded. |
| No global growth | Querying recent windows derives deterministic manifest keys and requires no growing global v2 index. |
| Background permissions | CampaignSessionReady uses only `Evaluate`; no permission request/UI path is invoked when access is unavailable. |
| Baseline evidence | Candidate checkpoint records BuildId, HEAD `f8cfc22c93dc28fc3fac69079e32c46dd9bef457`, and hashes of pre-existing dirty files before implementation. |
