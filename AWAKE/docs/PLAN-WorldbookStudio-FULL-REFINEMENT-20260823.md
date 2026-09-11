# Plan: Worldbook Studio 全量精进批次
_Locked via grill — by Codex + user autonomous execution authorization; final review approved_

## Goal

在不改变世界书 Schema、现有 CLI 命令、HTTP 路由、AI Provider 请求/结果契约、保存格式和 AWAKE 游戏运行时边界的前提下，完成 Worldbook Studio 的剩余可证实精进：减少同一操作中的重复读取与重复解析，稳定验证快照和发布边界，逐步降低高复杂度代码的维护风险，继续提升中文编辑器和 AI 辅助的可理解性，并用完整测试、打包和债务审计证明没有回归。A1 单一权威整理视为已完成基线。

## Approach

1. 固定 A1 基线：保留 A1 checkpoint、golden vectors、CLI/Web authority smoke 和当前离线包哈希；后续批次不覆盖 A1 证据，新增证据必须与 A1 named test/fixture 对照。
2. A2 — Validated Snapshot 与 I/O 边界：以“单次 public application 调用”为 operation context 边界；`Validate`、`Compile`、`Preview`、`Export` 各自从同一入口创建一个不可变 `ValidatedSnapshot`，内部 helper 只接收该 snapshot，不跨 HTTP/CLI 请求共享可变缓存，也不新增 public 命令或路由。输入闭包明确包含 authoring 正典文档、source registry 文件、audit events、identity ledger，以及实际加载的 schema 文件；每个输入都由一次 byte read 同时完成 UTF-8 解析与 SHA-256，组装后做路径集合和 bytes hash 复核，最多从头重建一次，第二次变化复用既有 `WB-CAS-409`。输入闭包指纹绑定 path/hash、schema hash、tier、fixture/profile 和 revision；快照后的 Compile/Preview/Export 只消费内存对象。为单文件读取中、快照组装后、输出前三个时间点增加 TOCTOU fixture、读取计数和旧候选/current pointer 保护测试。
3. A3 — Core 复杂度精进：拆成独立的 A3.x 子批次，不在一个子批次同时改模板构造、content graph、preview 和 ValidationServices。每个子批次先记录精确入口/方法集合、变更预算和 characterization golden，再只抽取已有调用图证明的纯 seam；不拆 public application service，不引入 Mediator/Repository/DI，不改变错误码、诊断顺序、输出字段或 hash。
4. A4 — 入口与安全边界整理：拆成 A4-CLI/Web 与 A4-Launcher 两个子批次。CLI/Web 只合并已证明等价的适配逻辑，保留 CLI snake_case 与 Web camelCase 的独立 DTO、退出码/HTTP 状态和错误消息；Launcher 子批次先冻结现有协议字段和 lifecycle：`StudioBootstrap` 的 protocol/instance/nonce/workspace/schema/package、固定 loopback port、`/health` 响应、shutdown proof、mutex、job/process cleanup、环境变量和浏览器 fallback，再做等价整理。Token、Nonce、CSRF、Consent、Provider session、Document CAS 的生命周期不合并。
5. A5 — 编辑器可用性精进：拆成 A5-UI 与 A5-AI/CAS 两个子批次。A5-UI 只处理中文模块化表单、字段说明、必填/自动生成/只读标记、枚举帮助、事实/表达/权限卡片、错误定位和 dirty state；A5-AI/CAS 只在单独契约冻结后处理建议缓冲区、人工复核和 CAS 状态。两批都保留高级 YAML/JSON 模式，不改变机器契约；用固定 DOM selector、中文文案键和本地 fixture 验证。
6. A6 — 发布与维护闭环：仅在 A2、全部 A3.x、A4-CLI/Web、A4-Launcher、A5-UI、A5-AI/CAS 通过各自门槛后执行。每次命令生成 UTC `run_id`，记录命令、输入/输出 hash、退出码、路径、证据等级和源码/包指纹；重复跑完整 harness、CLI/Web smoke、package/Launcher success/failure smoke、前端契约解析和债务审计，不同步游戏目录。

## Phase gates and bounded contracts

- A2、A3.x、A4-CLI/Web、A4-Launcher、A5-UI、A5-AI/CAS、A6 都是独立批次；每一批单独记录 changed files、focused tests、checkpoint、证据等级和债务审计。A2 未通过不得开始 A3.1；每个 A3.x 未通过不得开始后续 A3.x；A3 全部通过后才开始 A4/A5；A6 最后执行。
- A2 的 operation context 明确为单次 `WorldbookApplicationService` public 方法调用；跨请求、跨进程和长期全局缓存一律不属于本批。`ValidatedSnapshot` 的输入闭包是按当前 Workspace 读取规则枚举的 authoring 文档、source registry、audit events、identity ledger 和实际使用的 schema 文件；compiled/export/report 目录只有在代码明确读取时才纳入，生成的 ValidationReport 不冒充输入。snapshot 同时保存原始 bytes、UTF-8 解析对象、单文件 hash、规范化相对路径集合、完整 fingerprint、registry/source/audit/ledger/schema hash、tier、fixture/profile、revision 和生成时刻。
- A2 每个文件先做一次有界 stable byte read：从同一 byte buffer 计算 hash、解析和报告 input hash；不能出现先 `ReadAllText` 再对磁盘文件另算 hash。组装完成后重新枚举闭包并按规范化相对路径比较路径集合和 bytes hash；文件新增、删除、重命名、内容变化、解析失败、revision 回退或 registry/source/audit/ledger/schema 变化均丢弃本次 snapshot，最多从头重建一次，第二次仍变化返回现有 `WB-CAS-409`，不生成编译/导出结果。Windows 路径比较不区分大小写，fingerprint 不包含时间戳。
- A2 的三个 TOCTOU seam 固定为：单文件 byte read 期间、snapshot 组装/复核结束后、输出边界前。每个 seam 都必须证明失败或最多一次从头重建；异常路径不得改变旧候选、current pointer、已存在的 compiled/export 文件。`WorkspaceReadProbe` 只观察输入闭包读取次数，证明 snapshot 交付给下游后不再重复读盘；复核读取属于 snapshot 建立阶段，不计入下游。
- A1 保护矩阵固定记录在测试或 fixture 中：canonical/hash vectors、CLI snake_case 输出、Web camelCase 输出、HTTP 状态/错误码、安全消息、Provider 字段、保存 round-trip、路由名和 package manifest；每项写明 named test、fixture、expected artifact 和比较方式，golden 不得由当前实现自动重生成。
- A1 矩阵必须落为受版本控制的 `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\tools\worldbook-studio\tests\fixtures\a1-protection-matrix.v1.json`：恰好 72 个唯一行，每行包含 `id`、named test、fixture、expected artifact、比较方式和契约域；测试必须机检 `72/72`，不得运行时从当前测试代码自动生成矩阵。
- A2 必须有 instrumented read-inventory fixture：在 `WorkspaceService`、schema validator、registry/source/audit/ledger loader 的测试 seam 记录每次规范化相对路径读取；snapshot 输入闭包清单与实际读取清单做集合相等比较，任何未声明读取、漏列文件、目录新增/删除或 schema loader 旁路读取都失败，并把 closure manifest/hash 写入 focused test artifact。
- A2 无竞态基线的精确读取上限固定为：`Validate` 对每个闭包文件最多 2 次（初始 stable byte read + 组装后复核），`Compile`/`Preview`/`Export` 对每个闭包文件最多 3 次（再加输出边界复核）；snapshot 交付给下游后为 0 次读盘。允许的一次从头重建按同样上限重新计数，第二次变化直接 `WB-CAS-409`；每个 operation/path/category 的计数和上限必须写入测试，不得只断言总数。
- A2 的 derived output 必须先写入 workspace 内随机 staging 路径，完成 snapshot fingerprint 复核后再以现有写入策略原子发布；输入在 staging 或提交边界变化时丢弃 staging、不替换旧 compiled/export/current pointer，并记录 `WB-CAS-409`。提交后立即做输出 manifest/hash 与 snapshot fingerprint 的只读复核；失败路径必须证明旧候选和 current pointer 保持不变。
- A3.x 的 hash/输出比较使用现有 `CanonicalJson.Serialize`、UTF-8 无 BOM、LF、稳定字段排序和既有 nondeterministic-field 排除规则；比较对象为 byte/hash 或结构化字段的明确集合，不以“重新生成后相同”替代基线。
- A4-CLI/Web 只审查 `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\tools\worldbook-studio\src\Awake.WorldbookStudio.Cli\Program.cs`、对应 Web `Program.cs` 的既有 `/api/*` endpoints；A4-Launcher 只审查 `WorkspaceManager.cs`/`WebProcessHost.cs` 及其现有 smoke。不得设计新安全状态机，不合并 Token/Nonce/CSRF/Consent/CAS 生命周期。
- A4-CLI/Web 验收明确比较 CLI snake_case fixture、Web camelCase fixture、语义字段集合、错误码、安全脱敏消息、CLI 退出码和 HTTP 状态；不得把“契约一致”解释为字段命名相同。A4-Launcher 验收固定比较 `StudioBootstrap` 字段与顺序、protocol version、loopback port、`/health` 状态/字段、shutdown proof header/计算、mutex 错误、job/process cleanup、环境变量和浏览器 fallback 的结果码与日志事件。
- A5-UI 验收使用固定 DOM selectors、中文文案/帮助键、required/read-only/generated 状态、错误 path、dirty-state 事件和至少一份 YAML/JSON round-trip fixture；A5-AI/CAS 另行固定 request/response schema、request/source hash、revision、session/consent/nonce/buffer/apply/reject 错误映射和人工确认边界。“用户友好”只作为这些可观测断言的集合，不作为单独主观结论。
- A6 每次运行生成 UTC `run_id`，命令、输入包 hash、输出路径、退出码、源码/包指纹和证据等级写入 `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\docs\checkpoints\` 与 `C:\Users\26811\OneDrive\文档\New project\tools\code-debt-audit\reports\`。Studio 脚本的工作目录固定为 `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\tools\worldbook-studio`，标准命令固定为 `.\scripts\test.ps1`、`.\scripts\a1-authority-smoke.ps1`、`.\scripts\package.ps1 -Output artifacts\WorldbookStudio -RunSmoke`、`.\scripts\smoke.ps1 -Zip artifacts\WorldbookStudio-win-x64.zip -Browser failure`；其中 package 的 `-RunSmoke` 必须保留 Launcher success 结果 `WB-SMOKE-PASS`、`browserOpened=true`、`exited=true`，独立 failure smoke 必须保留 `WB-BROWSER-OPEN-FAILED`、`browserOpened=false`、`exited=true`，两份结果归档到同一 `run_id`。债务审计从工作区根执行 `.\tools\code-debt-audit\Invoke-CodeDebtAudit.ps1`。当前 build gate 为 Release `0 warnings / 0 errors`；若环境出现既有 warning，必须报告 delta，不得隐藏。
- A6 证据格式固定为 `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\docs\checkpoints\worldbook-studio-evidence-manifest.v1.schema.json` 约束的 JSON；`run_id` 使用 UTC `yyyyMMdd'T'HHmmss'Z'`，文件名为 `worldbook-studio-{batch_id}-{run_id}.json`。必填字段为 `schema_version`、`run_id`、`batch_id`、`started_at_utc`、`finished_at_utc`、`working_directory`、`commands[]`、`source_tree_hash`、`package_hashes`、`artifact_paths`、`exit_codes`、`warning_baseline_path`、`warning_delta`、`evidence_level`、`unverified` 和 `game_directory_touched=false`；每个 command 记录完整命令、cwd、退出码、stdout/stderr 路径、输入/输出 hash。A6 必须显式运行 `.\scripts\release-check.ps1 -Package artifacts\WorldbookStudio`，不得用 package 内部调用替代独立证据。

## Key decisions & tradeoffs

- 优先处理“同一操作重复 I/O”和“验证快照不一致”，不依据静态复杂度分数盲目重构。
- 不把不同生命周期的 CLI Consent、Web Session、Token、Nonce、CSRF、CAS 合成一个状态机。
- Web/CLI 的 wire DTO 继续分开；Core 只共享内部语义和纯逻辑。
- 不用全局长期缓存保存世界书内容；工作区文件变化必须可检测，缓存不能压过磁盘事实。
- 编辑器用户体验提升通过中文视图、帮助和诊断完成，不把内部英文 ID 暴露为默认编辑内容，也不让 AI 自动写入正典。
- 本轮以行为保持为主；任何 Schema、路由、命令、AI 字段、错误码或哈希语义变化都另立兼容批次。

## Acceptance contract

- A2：单次 operation context、完整输入闭包 fingerprint、同一 bytes 的 hash/parse、三阶段 TOCTOU、最多一次重建/`WB-CAS-409`、旧候选保护和 `WorkspaceReadProbe` 读取计数均有 named tests；A1 的全部 `72/72` 和保护矩阵保持通过。
- A2：另有受版本控制的 72 行 A1 保护矩阵、实际读取集合等于声明闭包的 inventory manifest、按 operation/path/category 的精确读数上限、staging/atomic publish/post-commit hash 复核和旧 pointer 保护证据；任何额外磁盘读取或未登记输入都使批次失败。
- A3.x：每个子批次只有一组明确 seam；characterization golden 比较 canonical bytes/hash、字段集合、诊断顺序、manifest/preview 输出和异常边界，且单批 changed-file/方法预算不超出计划。
- A4-CLI/Web：指定 command switch/endpoints 的成功/失败/禁用路径都有命名测试；CLI/Web 各自 wire casing、语义字段、HTTP/退出码、错误码和脱敏消息保持基线。A4-Launcher：bootstrap、health、shutdown、mutex、job/process cleanup、browser fallback 均有命名 success/failure smoke，且不改变 Token/Nonce/CSRF/CAS 生命周期。
- A5-UI：固定 selector 能找到中文字段标签、帮助、required/read-only/generated 标记；空值/非法枚举/保存失败能定位到模块和字段；dirty-state、保存和 YAML/JSON round-trip 通过。A5-AI/CAS：请求/响应 schema、hash/revision/session/consent/nonce/buffer/apply/reject 及人工复核断言通过。
- A6：每个命令有 `run_id`、退出码、输入/输出 hash、源码/包指纹、证据等级和报告路径；Release build、A1 `72/72`、各批 focused tests、CLI/Web smoke、package/release-check、Launcher success/failure smoke 和债务审计均有新鲜证据；游戏目录和冻结候选未改变。
- A6：除上述结果外必须生成并通过 `worldbook-studio-evidence-manifest.v1.json` schema 校验，含固定 UTC run_id、warning baseline/delta、显式独立 release-check 命令和所有输出文件 hash；缺少任一字段不得归档为完成。

## Risks / open questions

- 现有代码是单文件 application service 与单文件测试 harness；拆分过度可能增加入口和维护成本，必须每批只抽取已有调用图证明的 seam。
- 静态 I/O/复杂度审计不能证明性能收益；没有 before/after 测量时只报告风险消除，不宣称性能提升。
- 本计划不作未测量的速度、内存、并发或发布质量提升声明；只有 profiling/load evidence 才能宣称性能变化。
- 前端 smoke 不等于人工编辑体验完全通过；仍需记录用户手动启动包后的视觉/操作反馈。
- 真实云端 Provider、真实本机 Worker、Bannerlord 实机、存档和游戏目录同步不在本轮自动验证内。
- 所有受这些外部环境影响的契约在每个 checkpoint 中明确标为 `unverified`，不会被 Studio 离线 smoke 代替。

## Out of scope

- 不启动 Bannerlord，不执行 `sync_module.ps1`，不修改 `Modules\\AWAKE`、`PlayerExports`、dist、冻结或待验运行候选。
- 不改变世界书 Schema、运行时读取器、CLI 命令名、HTTP 路由、AI Provider schema、保存 key 或发布目录契约。
- 不实现 NPC 主动学习、周报传播、游戏内玩家世界书编辑的新运行时机制；本计划只完善 Studio 和既有契约。
- 不调用真实云端 API，不写入真实 API Key，不把本机假 Worker smoke 当作真实外部兼容证明。
