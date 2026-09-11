# Plan: Worldbook Studio A3.4 Registry validation projection seam
_Locked via grill — by Codex + user autonomous execution authorization; independent read-only review `VERDICT: APPROVED`_

## Goal

在不改变 `RegistryService.LoadAndValidate` 的文件读取、SnapshotInputStore 取字节、Schema 校验、诊断顺序、空注册表边界、profile/referral hash、编辑器目录或 Preview Envelope 的前提下，将注册表 JSON 已解析对象到 `RegistrySnapshot` 的纯内存投影与 profile parent-chain 诊断抽离为一个唯一内部 seam，并用旧实现 golden 证明 registry 输出保持一致。

## Scope and seam

1. 目标旧逻辑：`ValidationServices.cs` 中 `RegistryService.LoadAndValidate` 在两个 JSON 文件读取/解析和 `_schema.Validate` 之后构造 `RegistrySnapshot`、收集 profile/referral ID 集合、继承关系、公开 referral 集合、profile object 映射，并验证未知 parent/cycle。
2. 新增 `src/Awake.WorldbookStudio.Core/RegistrySnapshotBuilder.cs`，只包含一个 `internal static` 类型和一个 `internal static Build` 方法。`Build` 只接收已解析的 `profiles`/`referrals`、原始 profile/referral bytes 和 `ValidationReport`，不访问 `WorkspaceService`、`File`、`Directory`、网络或全局可变状态；返回 `RegistrySnapshot` 并按旧顺序追加 parent-chain 诊断。
3. `RegistryService.LoadAndValidate` 只保留路径存在性、SnapshotInputStore/磁盘 byte read、JSON parse、两次 Schema validate、Builder 调用；缺失 registry 时通过 Builder 的空 JSON 输入保持现有 `WB-REGISTRY-000` 和空快照行为，不保留第二套 `RegistrySnapshot` 构造 authority。
4. 不改变 `RegistryService.GetProfileChain`、`SourceRegistryService`、`AuditLedgerService`、Application 快照生命周期、AuthoringEditorModel、Preview、CLI/Web、AI Provider/Worker、Launcher、Schema、游戏运行时或 MCM。

## Pre-extraction characterization

1. 在旧 `ValidationServices.cs` 未修改前，用固定 capture harness 固化五类 registry 输入：`baseline`（当前 profile/referral registry，版本/hash/ID/parent/public referral/profile object projection）、`unknown_parent`（新增 profile 继承未知 profile，验证 registry snapshot 仍构造且 `WB-PROFILE-001` 在 Schema 诊断之后追加）、`parent_cycle`（两个 profile 互相继承，验证每个 profile 的 cycle 诊断顺序和 detail）、`schema_invalid`（可解析但违反 registry Schema，同时保留未知 parent，验证 Schema 诊断先于 Builder parent 诊断）、`missing_registry`（删除一个 registry 文件，验证 `WB-REGISTRY-000`、空版本/hash/集合且无伪造 parent 诊断）。Golden 记录旧 `ValidationServices.cs` SHA-256、capture harness 源码 SHA-256、capture 命令、runtime 摘要、完整输入文件相对路径/hash/原始 bytes hash、原始 snapshot projection UTF-8 bytes/base64/hash、规范化 snapshot projection、诊断 code/severity/path/detail/order、registry hashes 和 canonical hash；golden 不由新 builder 运行时生成。
2. 只比较公开/可稳定投影：版本、profile/referral hash、按 registry 原始顺序的 profile/referral IDs、按 ID 排序的 parent map、publicly askable IDs、profile object canonical hashes，以及诊断序列；输入 profile/referral 数组的原始顺序、parent traversal 顺序和诊断顺序必须显式固定；测试不把 `HashSet`/`Dictionary` 枚举顺序当作契约，但 Builder 不得依赖集合枚举来决定诊断顺序。
3. 输入边界自包含：相对路径保持原样；绝对路径仅在 provenance 中规范化；跨临时 workspace 只比较输入相对路径、bytes hash、snapshot projection 和诊断 canonical hash。Schema-invalid registry 的 schema 诊断必须和 parent-chain 诊断分开记录，不能让 builder 诊断替代 Schema 诊断。任一 registry 文件缺失时，两个 registry 文件及其两个 registry Schema 均不读取，probe 读数保持零；分支明确传入两个空 JSON 对象和 `Array.Empty<byte>()` 给 Builder；普通存在分支保持 A2 每个输入 initial/assembly/output 各一次。

## Tests (exactly 3 new named cases)

当前 harness `93` 个 named cases；实现后必须为 `96`，新增精确行各出现一次：`Run("A3.4 registry snapshot golden", () =>`、`Run("A3.4 registry diagnostics and empty boundary", () =>`、`Run("A3.4 registry wiring and determinism", () =>`。移除三行后，原有 93 行 manifest hash 必须保持 `eb6e76fe216bddb066797e6bb77823387b72e14044face9bf865e352f889802c`，并由测试同时断言总数 `96`。

1. **A3.4 registry snapshot golden**：反射确认 `RegistrySnapshotBuilder` 为 internal 且只有一个 static `Build`；builder 源码无 Workspace/File/Directory/network 依赖；`ValidationServices.cs` 只调用 Builder 一次且不再包含旧 inline registry projection/parent traversal；精确断言 `ValidationServices.cs` 的 `RegistrySnapshotBuilder.Build(` 恰为 1 次、`new RegistrySnapshot`、`Profiles.Add(`、`Referrals.Add(`、`PubliclyAskableReferrals.Add(`、profile/referral 输入遍历循环、`ProfileParents[...] =`、`ProfileObjects[...] =`、`while (!string.IsNullOrWhiteSpace(parent))` 均为 0 次；Core 其他文件没有第二个旧 helper authority。对五个 golden case（包括 `schema_invalid`）比较 snapshot projection、profile/referral hash、对象 canonical hash、诊断顺序和 canonical hash。
2. **A3.4 registry diagnostics and empty boundary**：明确断言未知 parent、parent cycle 的 `WB-PROFILE-001` message/path/detail/order；Schema-invalid registry 仍保留 Schema 诊断并按旧顺序追加 parent 诊断；缺失 registry 只产生 `WB-REGISTRY-000` 并返回空快照；`GetProfileChain` 的现有继承顺序和未知 profile 行为不变。
3. **A3.4 registry wiring and determinism**：同一 workspace 连续两次 `RegistryService.LoadAndValidate` 的 projection、diagnostics、hash 和 key/array 顺序稳定；使用 `WorkspaceReadProbe` 的完整应用调用不增加 A2 既有三阶段读取上限；缺失 registry 分支固定验证不读取缺失 registry/schema 文件且仍返回 `WB-REGISTRY-000`；Builder 直接接收内存对象，不产生任何输入文件读取。

## Acceptance contract

- **Trigger:** 现有 `RegistryService.LoadAndValidate` 及三项 focused harness cases。
- **Observable result:** registry snapshot、profile chain、editor catalog/Preview registry fields、诊断顺序、错误码、hash 和空注册表行为与旧 golden 完全一致。
- **Invariants:** 不改 public API、CLI/Web wire casing、Schema、错误码、输入读取、SnapshotInputStore、`GetProfileChain` 语义、输出字段、hash 或 AWAKE runtime boundary；Builder 无 I/O、无全局状态。
- **Failure behavior:** registry 文件缺失仍产生 `WB-REGISTRY-000`；未知 parent/cycle 仍产生原 `WB-PROFILE-001`；Schema 诊断不被 Builder 诊断覆盖或重排；无 profile/referral 时返回与旧实现相同的空快照。
- **Evidence:** `96/96 PASS`；Release build `0 warnings / 0 errors`；A1 CLI/Web smoke `PASS`；existing package release-check `PASS`；21 worldbook-studio-plan JSON files parse；范围债务审计 `passed`；无 package rebuild、游戏启动或同步。
- **Non-goals:** 不拆 `SourceRegistryService` 或 `AuditLedgerService`，不改 registry schema、Editor UI、AI/CAS、Launcher、Preview、RuntimePackageCompiler 或游戏内读取器。

## Change budget and risks

- Production: 1 new Core file plus one `RegistryService.LoadAndValidate` call-site replacement/deletion; no more than 1 internal type and 1 method. Test/fixture: 1 golden and exactly 3 named cases plus manifest helper update.
- Preserve registry array order during collection, preserve report diagnostic order, do not sort input arrays inside Builder, and never infer semantic order from HashSet/Dictionary enumeration.
- Static complexity/I/O audit findings are advisory only; no performance claim without measurement.

## Wiring and provenance checks

- `ValidationServices.cs` must contain exactly one `RegistrySnapshotBuilder.Build(` call, zero `new RegistrySnapshot` expressions, zero `Profiles.Add(`, `Referrals.Add(`, `PubliclyAskableReferrals.Add(`, `ProfileParents[...] =`, `ProfileObjects[...] =`, profile/referral input traversal loops and `while (!string.IsNullOrWhiteSpace(parent))`; those projection/parent-traversal forms may appear only in `RegistrySnapshotBuilder.cs`. The scan root is the top-level `src/Awake.WorldbookStudio.Core/*.cs` set, with the builder file as the sole allowed file.
- `RegistrySnapshotBuilder.cs` must contain one `Build` declaration, no file/network APIs, and no dependency on `WorkspaceService` or `SchemaValidator`. It must iterate registry arrays directly for all observable ordering and never iterate a `HashSet`/`Dictionary` to produce diagnostics.
- `baseline_capture.archive_path` is `tests/fixtures/a3-4-registry-snapshot-golden.v1.json`; raw input hashes and old `ValidationServices.cs` hash must be embedded in that archive. The archive must include `raw_snapshot_projection_utf8_sha256`, `raw_snapshot_projection_base64`, and the full input byte/hash inventory.
