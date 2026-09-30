# Plan: Worldbook Studio A3.3 Preview projection seam

> 状态：**`implemented`（2026-09-30 标记）。**
> 依据：本批所属子系统已存在：`tools/worldbook-studio/` 含 `Awake.WorldbookStudio.{Cli,Core,Launcher,Web}` 与 entity registry；本系列为该子系统的内部行为保持型重构批次。
> ⚠️ 本文件是**批次施工单**，其所属子系统已存在 ⇒ 本批视为已完成。**保留本文件**（不是 `superseded`）——
> 它记录了当时的设计意图与边界，仍有追溯价值。
> 现行方向：`AWAKE-ROADMAP.md`；分诊依据：`AUDIT-PLAN-TRIAGE-20260930.md`。

_Locked via grill — by Codex + user autonomous execution authorization; independent read-only review `VERDICT: APPROVED`_

## Goal

在不改变 `WorldbookApplicationService.Preview` 的 profile/identity 权限语义、NPC DTO、作者诊断 DTO、fallback referral 过滤、诊断顺序、Schema 校验、`WB-PREVIEW-001` 行为、输入读取边界或 CLI/Web 契约的前提下，将 Preview 中唯一的纯内存“文档表达 → NPC 结果/作者诊断数组”循环抽离为一个内部 seam，并用旧实现 characterization golden 证明入口→权限结算→Envelope 输出保持一致。

## Scope and seam

1. 目标旧逻辑：`Application.cs:336–380` 的 profile chain 构造、documents/assertions/expressions 遍历、deny 优先、grant/layer 状态、公开 referral 过滤、NPC DTO 和 author diagnostic DTO 构造，以及其私有纯逻辑 helper `MatchingRules`、`MatchesDimension`、`LayerRank`、`SourceIds`。
2. 新增 `src/Awake.WorldbookStudio.Core/PreviewProjectionBuilder.cs`，只包含一个 `internal static` 类型和一个 `internal static Build` 方法；方法接受内存中的 documents、registries、profile ID 和 `IdentitySnapshot`，返回命名元组 `(JsonArray NpcResults, JsonArray DiagnosticResults)`。helper 改为方法内 local functions，避免保留第二套权限 authority。
3. `Preview` 只保留 public 生命周期：`BuildSnapshot`、默认 identity、未知 profile 早退、调用 builder、`BuildPreviewEnvelope`、Envelope Schema/语义校验、PreviewItem 投影、空结果 info、`VerifyForOutput`；不改变这些调用顺序或 I/O。
4. 不改变 `BuildPreviewEnvelope`、`ValidatePreviewEnvelope`、`ToPreviewItem`、`ValidatedSnapshot`、`PreviewResult`、Schema、路由、CLI/Web DTO、Provider/Worker、游戏运行时或 MCM；本批 MCM 不适用。

## Pre-extraction characterization

1. 在旧 `Application.cs` 未修改前，用固定临时 capture harness 运行九个输入：`baseline_commoner`（最小合法档案，visible/summary）、`denied_noble`（两个匹配 deny 与两个匹配 grant，验证 deny 优先/redacted、rule 顺序）、`unknown_public_referral`（grant 不命中、两个 publicly askable referral、source_id 重复但 source_ref 不同，验证 unknown/fallback/source 去重）、`rumor_commoner`（layer=rumor；现有基线 grant 的 `min_detail=summary` 高于 rumor 层，因此验证 status=unknown 但保留 layer=rumor）、`empty_rules`（合法空 grants/denies，验证 unknown 且无 referral）、`identity_match`（culture/kingdom/settlement 全命中，age/steward 在阈值内）、`identity_miss`（同一规则至少一个维度不命中、age/steward 低于阈值且 identity 字段不为空），以及两个 raw cases：`invalid_profile`（有效档案但 profile 不存在，验证早退/Envelope）和 `missing_text_raw`（表达缺少 text，验证 builder 的空 localized object 分支与 authoring snapshot Schema 诊断）。`missing_text_raw` 的 `npc-preview` Schema 仍按现有契约通过，因为 `text` 可选且空对象允许；不得新增 Envelope Schema 诊断。所有合法输入复用现有 source/registry bytes；raw cases 只比较 Preview snapshot/result 诊断与 Envelope，不声称 valid authoring 输入。
2. 受版本控制 golden/原始捕获归档唯一固定为 `tests/fixtures/a3-3-preview-golden.v1.json`；不依赖临时目录长期存在。capture harness 可在 `<TEMP>/awake-a3-3-preview-capture/Program.cs` 与 `capture.csproj` 运行，但 golden 的 `baseline_capture` 必须记录 harness 源码 SHA-256、capture 命令、旧 `Application.cs` SHA-256、SDK/runtime 摘要、完整输入文件相对路径和逐文件 SHA-256；每个 case 固定 `PreviewResult.Envelope` 完整对象、原始 Envelope UTF-8 bytes SHA-256/base64、Envelope key/array 顺序、`Items`、`snapshot_report_diagnostics`、`result_diagnostics` 的 code/severity/path/order、canonical hash 和 valid/status。golden 本身就是不可由新 builder 重生成的 raw capture archive。
3. 路径/hash 边界必须在 A3.3 自包含：相对值保持原样，不解析为 workspace 路径；绝对值先 `Path.GetFullPath`，Windows 同时接受 `\\` 与 `/`，root/candidate 去除尾部分隔符但保留磁盘根，仅当 candidate 与 root 大小写不敏感地完全相等，或以 `root + Path.DirectorySeparatorChar/Path.AltDirectorySeparatorChar` 开头时才归一化；root 替换为 `<WORKSPACE_ROOT>`，子路径替换为 `<WORKSPACE_ROOT>/` 加 `/` 分隔的相对路径，`C:\workspace2` 不匹配 `C:\workspace`。跨工作区只比较归一化结构/canonical hash；原始 Envelope bytes/hash 只用于旧实现 provenance 和同一 workspace determinism。

## Tests (exactly 3 new named cases)

当前 harness `90` 个 named cases；实现后必须为 `93`，且三个精确行各出现一次：`Run("A3.3 preview golden", () =>`、`Run("A3.3 permission matrix", () =>`、`Run("A3.3 preview wiring and determinism", () =>`。移除这三行后，剩余 90 行按 UTF-8、单 LF、无首尾换行连接的 manifest hash 必须仍为 `35468699f85151a83729e828bc792210e69c3489677deffe115f0160f01a089f`；测试还必须断言总 named-case 数为 93，防止只通过静态旧 hash 而漏掉新增用例。`880ce9b5f90216094ede7b441063ad4bb8ec567afe3c6f931490e6e4bc78e5c4` 是移除 A3.2 三个用例后的 87-case 基线，不是本批次的 90-case 基线。

1. **A3.3 preview golden**：反射确认 `PreviewProjectionBuilder` 为 internal 且源码无 `WorkspaceService`/`File`/`Directory`/网络依赖；源码静态断言 `Application.cs` 对 `PreviewProjectionBuilder.Build` 恰有一个调用，`Application.cs` 中旧 inline `foreach (var item in snapshot.Documents)`、`MatchingRules(`、`SourceIds(`、`LayerRank(` 的出现次数均为 0；在整个 Core 目录中，除 `PreviewProjectionBuilder.cs` 外这些名称出现次数为 0，builder 内每个 local function 定义/调用位置由源码检查固定，防止第二 authority；对全部九个 case 比较 Envelope、NPC/diagnostic 完整对象、顺序、key sequence、canonical hash、Items、snapshot report diagnostics 与 result diagnostics。
2. **A3.3 permission matrix**：明确断言 deny 优先、多个 deny/grant rule ID 顺序、grant 命中后的 `visible/rumor`、低于 `min_detail` 时保留 layer 但返回 `unknown`、未命中时 `unknown` 与只保留 publicly askable referral、source ID distinct 顺序、空 grants/denies、culture/kingdom/settlement 全维度命中与不命中、age/steward 阈值内/外、未知 profile 早退 `WB-PROFILE-001`、missing-text raw 分支的空 localized object且无新增 Envelope Schema 诊断；分别比较 seam 生成的 `author_diagnostics.results`、`snapshot.Report.Diagnostics` 和最后返回的 `PreviewResult.Diagnostics`，不把三者混为一个数组。
3. **A3.3 preview wiring/determinism**：同一 workspace/profile/identity 两次 Preview 的 Envelope canonical bytes/hash、Items、diagnostics 和 snapshot read inventory 稳定；snapshot Envelope 与 DTO 解析等价，且不增加 A2 既有读取上限。

## Acceptance contract

- **Trigger:** 现有 `WorldbookApplicationService.Preview(profileId, fixtureId, identity)` 及三项 focused harness cases。
- **Observable result:** NPC 预览、作者诊断、Envelope、Items、diagnostic 顺序/错误码/状态、fallback referral 和 canonical output 与旧 golden 完全一致。
- **Invariants:** 不改 public API、CLI/Web wire casing、Schema、错误码、输入读取、权限优先级、层级排名、referral 公开过滤、`VerifyForOutput` 时机或 AWAKE 运行时边界；builder 无 I/O、无全局可变状态。
- **Failure behavior:** 未知 profile 仍早退并产生 `WB-PROFILE-001`；deny 仍遮蔽 grant；未命中权限仍返回 unknown 和允许公开询问的 referral；低于 `min_detail` 的 rumor 仍保持 unknown 但保留原 layer；空 grants/denies 不产生 referral；合法 preview 无可见 Items 时仍追加 `WB-PREVIEW-001` info；missing-text raw case 仍由 builder 生成空 localized object，authoring snapshot 保留原 Schema 诊断，`npc-preview`/Envelope Schema 不新增诊断；snapshot report、author diagnostics 和 result diagnostics 的来源/顺序不互相替代。
- **Evidence:** `93/93 PASS`；Release build `0 warnings / 0 errors`；A1 CLI/Web smoke `PASS`；existing package release-check `PASS`；worldbook-studio-plan JSON parse `PASS`；范围债务审计 `passed`；无 package rebuild、游戏启动或同步。
- **Non-goals:** 不重构 Preview Envelope、ValidationServices、AuthoringEditorModel、AI/CAS、UI、Launcher、Schema、RuntimePackageCompiler 或游戏内读取器。

## Change budget and risks

- Production: 1 new Core file plus one call-site replacement/deletion; no more than 1 internal type and 1 method. Test/fixture: 1 golden and exactly 3 named cases plus manifest helper update.
- Preserve document iteration order, expression order, rule order, referral distinct order and JSON property insertion order; any sorting or canonicalization inside the builder is forbidden.
- Static complexity or I/O audit findings are risks only; no performance improvement claim without measurement.

## Wiring and provenance checks

- `Application.cs` 必须包含 `PreviewProjectionBuilder.Build(` 恰好 1 次，且不包含旧 preview projection loop 或 `MatchingRules(`/`MatchesDimension(`/`LayerRank(`/`SourceIds(`；整个 Core 目录除 `PreviewProjectionBuilder.cs` 外这些名称出现次数为 0，builder 文件只允许一个 `Build` 方法和其 local functions。
- `baseline_capture.archive_path` 固定为 `tests/fixtures/a3-3-preview-golden.v1.json`；临时 capture source/path 只作为 provenance metadata，raw Envelope bytes/base64、逐文件输入 hash、旧 application hash 和 harness hash 必须已经写入该 golden，删除临时目录不得损失证据。
