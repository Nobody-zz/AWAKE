# Plan: Worldbook Studio A3.2 内容图构造 seam

> 状态：**`implemented`（2026-09-30 标记）。**
> 依据：本批所属子系统已存在：`tools/worldbook-studio/` 含 `Awake.WorldbookStudio.{Cli,Core,Launcher,Web}` 与 entity registry；本系列为该子系统的内部行为保持型重构批次。
> ⚠️ 本文件是**批次施工单**，其所属子系统已存在 ⇒ 本批视为已完成。**保留本文件**（不是 `superseded`）——
> 它记录了当时的设计意图与边界，仍有追溯价值。
> 现行方向：`AWAKE-ROADMAP.md`；分诊依据：`AUDIT-PLAN-TRIAGE-20260930.md`。

_Locked via grill — by Codex + user autonomous execution authorization; final independent read-only review `VERDICT: APPROVED`_

## Goal

在不改变 `Compile`/`Validate` 的 graph 输出、节点与边顺序、去重规则、adult-tier 闭包、`WB-TIER-001` 诊断、报告顺序、content hash、Schema、CLI/Web 契约或 AWAKE 运行时边界的前提下，将 `WorldbookApplicationService.BuildContentGraph` 抽离为一个唯一的内部纯构造 seam，降低 application service 的复杂度并用固定 graph golden 证明入口→快照→编译输出行为保持一致。

## Approach

1. 固定三类基线输入：沿用现有 `MakeWorkspace(schemaRoot, "fixture-valid-minimal.yaml")` 及其 `authoring/sources/source-demo.yaml`、`source-demo.txt` 作为合法行为保持基线；在测试临时工作区内通过既有 fixture 文本变换构造一个仍符合 authoring Schema、包含 `redirects`、`fallback_referral_ids`、重复来源引用边和重复节点 ID 的 graph-branch 变体；另构造两个明确标记为“原始无效分支”的变体：文档级 `content_tier: unknown`，以及表达级额外字段 `content_tier: unknown`。后两者只用于证明旧 graph 构造在 Schema 错误存在时的 snapshot/诊断行为，不用于声称 Compile 文件输出有效。profile/referral registry 仍锁定 `docs/worldbook-studio-plan/profile-registry.v1.json` 与 `referral-registry.v1.json`、版本 `1.0.0` 及 A3.1 已核验的 bytes hash。实施前先使用旧 `Application.cs` 运行固定的临时 baseline-capture harness，命令、输入文件列表、旧 `Application.cs` SHA-256、运行时/SDK 版本和输出路径写入 `tests/fixtures/a3-2-content-graph-golden.v1.json` 的 `baseline_capture` 节点。路径归一化只作用于 graph 节点的 `origin_pointer`、诊断对象的 `path`/`detail`、以及用于结构比较的 `content-graph.json` 解析对象：相对路径值保持原样，不解析为 workspace 路径；绝对路径输入先用 `Path.GetFullPath`（Windows 下同时接受 `\\` 与 `/`）规范化，再将 workspace 根和 candidate 都去除尾部分隔符（但保留磁盘根），仅当 `candidate` 与 `root` 大小写不敏感地完全相等，或 `candidate` 以 `root + Path.DirectorySeparatorChar`/`Path.AltDirectorySeparatorChar` 开头时，才视为根或子路径。根值替换为 `<WORKSPACE_ROOT>`，子路径替换为 `<WORKSPACE_ROOT>/` 加 `/` 分隔的相对路径；`C:\workspace2` 不得匹配 `C:\workspace`。非当前 workspace 根及其子路径的字符串保持原样，不对任意文本做全局子串替换。golden 记录归一化后的完整对象、ordered key/array 序列和 canonical hash，同时保留合法 baseline/branch/adult_optional 的原始 `content-graph.json` bytes/hash 作为抽取前 provenance。跨工作区只比较归一化结构与归一化 canonical hash；同一工作区内的原始 graph bytes/hash 和 manifest/content hash 由 determinism 测试比较。该文件由旧实现输出后人工核对并固定，不允许由新 builder 运行时生成。
2. 新增 `src/Awake.WorldbookStudio.Core/ContentGraphBuilder.cs`，只承接原 `BuildContentGraph` 的对象构造逻辑；保留参数类型/顺序（包括当前虽未直接使用的 `sources` 与 `registries`）、本地 `Node`/`Edge` 去重逻辑、遍历顺序、unknown/adult_optional tier 判断、`WB-TIER-001` 报告调用和 graph 字段插入顺序。类型为 `internal`，方法只接受内存对象与 `ValidationReport`，不得引用 `WorkspaceService`、`File`、`Directory`、网络或全局可变状态。
3. 在 `Application.cs` 的 `BuildSnapshot` 中仅把单一调用替换为 `ContentGraphBuilder.Build`，删除旧私有方法；保留随后 `content-graph.v1.schema.json` 验证、`ContentGraph`/`HasAdultClosure` 写入 snapshot、Compile 文件编码和报告 finalize 的顺序。
4. 新增受版本控制的 `tests/fixtures/a3-2-content-graph-golden.v1.json`，固定独立人工确认的 graph canonical hash、root/property order、节点/边完整对象树、node/edge key sequence 和预期 tier/diagnostics；golden 不得由当前实现运行时重生成，canonical hash 不替代顺序断言。
5. 在现有单一测试 harness 中只增加 3 个 named cases（当前 `87` 变为 `90`，入口/输出格式不变）：
   - A3.2 graph golden：使用基线 fixture 和 graph-branch 变体，先将 snapshot graph 与 `content-graph.json` 中工作区根路径规范化为 `<WORKSPACE_ROOT>`，再断言 internal builder seam 存在且无 I/O 依赖，Compile snapshot graph 与独立 golden 的完整节点/边对象、顺序、字段顺序、规范化 canonical hash 和 `hasAdultClosure` 一致；branch 变体必须覆盖 `redirects`、`fallback_referral_ids`、来源引用、重复节点/边的首次出现去重与完整遍历。
   - A3.2 tier closure/diagnostics：分别覆盖一个合法的文档级 `adult_optional` 变体，以及两个原始无效分支（文档级 `content_tier: unknown`、表达级额外 `content_tier: unknown`），并按实际执行顺序固定诊断矩阵：文档级 `unknown` 先产生 Schema 诊断，再由 graph 构造阶段产生一条 `WB-TIER-001`（路径为该文档路径）；表达级 `unknown` 先产生 Schema 诊断，graph 构造只设置 `HasAdultClosure`，不新增 graph-stage `WB-TIER-001`；合法文档级 `adult_optional` 不产生 Schema/graph-stage tier 诊断。对所有变体固定 snapshot 的 `HasAdultClosure`、完整诊断 code/path/order；仅对合法 `adult_optional` 变体执行 `Compile(adult_optional, validToken)` 并比较 `content-graph.json`，对 base Compile 固定现有 `content-graph` gate 的 `WB-TIER-001`；原始无效分支只比较 snapshot，不要求不存在的 compiled 文件或 valid 结果。
   - A3.2 compile wiring/determinism：仅使用合法 baseline、graph-branch 和文档级 `adult_optional` 输入，证明 graph 被写入 `content-graph.json`，同一工作区内两次 Compile 的原始 graph bytes/hash 与 manifest/content hash 稳定，且规范化后的 snapshot graph 与输出文件解析对象等价；原始无效分支不进入本测试。
   既有 `87` 个测试、A1 `72/72` 保护矩阵和 A3.1 golden 不修改。
6. 在实现前后对 harness named-case 清单做机器核验：所有匹配 `^Run\("` 的完整源代码行按源顺序、UTF-8、单 LF 连接且无首尾换行；实现前 `87` 行基线 hash 固定为 `880ce9b5f90216094ede7b441063ad4bb8ec567afe3c6f931490e6e4bc78e5c4`，实现后仅移除 3 个新 A3.2 行比较该 hash，并断言总数 `90`。
7. 运行 focused harness、Release build、既有只读 A1 CLI/Web smoke、Contract JSON parse、占位符审计、`package/release-check` 质量门和范围债务审计；不修改 CLI/Web 生产代码、不打包、不启动游戏、不同步游戏目录或冻结候选。若质量门脚本要求真实发布包或冻结候选输入，则只以本批离线源码/测试范围执行并在 checkpoint 明确记录豁免原因，不伪造发布验证。

## Key decisions & tradeoffs

- 选择 `BuildContentGraph` 而不是 `Preview` 或 `ValidationServices`：它有唯一调用方、是纯内存构造、输出直接有 Schema 和 compile golden，且 adult-tier 逻辑有现有 F09/Compile 证据可保护。
- 保留 `sources`/`registries` 参数，即使当前方法体未直接读取它们；本批只做行为保持的 seam 抽取，不把“未使用参数清理”混入结构重构。
- 新 seam 为 `internal` 静态 builder，不引入 graph cache、Repository、DI、Mediator 或第二套 graph authority；`Application.cs` 仍是 public 流程入口，builder 只有一个权威实现。
- 测试项目当前不依赖 `InternalsVisibleTo`；通过受控反射与源码静态检查断言 seam 为 `internal static`、参数签名保持、实现文件不含 `File`/`Directory`/网络调用。除非编译器确实需要访问内部类型，否则不新增可见性契约。
- 对 graph 同时比较工作区路径规范化后的 canonical hash、完整对象树、节点/边顺序、JSON 文本 key sequence 和 array order；跨工作区不比较原始 bytes/hash 或 manifest/content hash，原始值只作为旧实现 provenance 或同一工作区 determinism 证据，不把机器路径当成跨工作区契约。
- 只报告静态复杂度风险的结构变化，不宣称性能收益；没有 before/after 测量就不做性能结论。
- MCM 评估：本批只抽离 Studio Core 的离线纯内存图构造，不改变 Bannerlord 配置、运行时选项或游戏入口，MCM 不适用且不修改任何 MCM 契约。

## Acceptance contract

- **Trigger:** 调用现有 `WorldbookApplicationService.Compile()`，以及现有测试 harness 的 tier 变体。
- **Observable result:** 对合法输入，Compile snapshot 的 `ContentGraph`、`HasAdultClosure`、规范化后的 `content-graph.json` 结构以及节点/边完整顺序、字段顺序、去重、diagnostics code/path/order 与独立 golden/既有预期完全一致；原始 `content-graph.json` bytes/hash 与 manifest/content hash 只在同一工作区的 determinism 测试中保持一致，不作为跨工作区 golden 契约。
- **Invariants:** 不新增或改变 public CLI 命令、HTTP 路由、Schema、错误码、诊断顺序、输出字段、输出 hash、A1/A2/A3.1 fixture、游戏目录或运行时契约；builder 无 I/O、无全局可变状态。
- **Failure behavior:** 文档级非法 `unknown` 仍按现有顺序产生 Schema 诊断、graph-stage `WB-TIER-001` 并进入 adult closure；表达级非法 `content_tier: unknown` 仍产生 Schema 诊断、只进入 adult closure 且不新增 graph-stage `WB-TIER-001`；这两个 Schema-invalid 分支只验证 snapshot 的 graph/closure/诊断，不生成或断言 compiled 文件。合法文档级 `adult_optional` 进入 adult closure但不产生 Schema/graph-stage tier 诊断；对它执行 base Compile 时仍由 `content-graph` gate 拒绝，对它执行无 confirmation token 的 adult_optional Compile 时仍由 `WB-CONFIRM-001` gate 拒绝，只有有效 token 路径进入 compiled 输出和 determinism 测试；报告 finalize 和下游写文件顺序不变；不新增 fault-injection infrastructure。
- **Evidence:** A3.2 harness `90/90 PASS`；existing-case manifest `87` 保留且仅增加 3 个；Release build `0 warnings / 0 errors`；A1 smoke `PASS`；债务审计 `passed`；源码 diff 仅限计划/日志、builder、调用点、graph golden、测试和 checkpoint。
- **Non-goals:** 不重构 Preview、ValidationServices、Publisher、编辑器 UI、AI/CAS、Launcher、Provider、Worker、Schema、Workspace I/O 或 Bannerlord 运行时。

## Risks / open questions

- Graph 节点/边顺序来自输入文档、数组和首次出现去重；任何先排序或改变遍历顺序都会改变 content hash，必须由 golden 捕获。
- 抽取前基线必须在旧实现仍存在时产生；固定文件为 `tests/fixtures/a3-2-content-graph-golden.v1.json`，其 `baseline_capture` 必须包含旧 `Application.cs` SHA-256、捕获命令、输入文件相对路径和 `dotnet --info` 摘要。抽取后测试不得只依赖新 builder 自己生成 golden，而要把旧实现留档的 graph、bytes/hash、诊断矩阵逐项对照；无效分支只对照 snapshot，不伪造 `content-graph.json`。
- `sources`/`registries` 当前参数未直接读取，不能借 seam 顺手删除；动态反射/未来调用者未知时保留签名。
- Harness 为单文件顶层程序；新增 3 个 named cases 必须保留现有入口、输出格式和 87-case manifest。
- 本批只证明离线 Studio Core/CLI/Web 契约，不证明 Bannerlord 实机、存档、云端 Provider、本机 Worker 或游戏目录同步。

## Change budget

- Production: 1 new Core file plus one existing call-site replacement/deletion; no more than 1 internal pure builder type and 1 method.
- Tests/fixtures: 1 graph golden fixture, exactly 3 focused harness cases and one read-only named-case manifest check; no unrelated fixture rewrites.
- No package rebuild or artifact overwrite in A3.2.
