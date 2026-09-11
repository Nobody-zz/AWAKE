# Plan Review Log: Worldbook Studio 全量精进批次

Act 1 (grill) complete — scope fixed by the existing A1 checkpoint, current code evidence, and the user's autonomous execution authorization. MAX_ROUNDS=3.

## Round 1 — Codex

**Material Issues**

- A2 未定义 snapshot 的完整输入闭包、key、生命周期、无下游重读规则和失效矩阵。
- 缺少 TOCTOU 测试，无法证明输入在验证后变化时不会导出混合内容。
- A1 保护契约没有逐项 machine-checkable matrix；A3 hash 比较规则不够明确。
- A2–A6 风险域过宽，A4 入口范围和 A5 用户友好验收过于主观。
- A6 缺少固定命令、run id、产物路径、退出码和 warning baseline 规则。
- 外部云端/Worker/Bannerlord 边界需要在每批 checkpoint 明确标为未验证。

### Claude's response

已接受并修订：增加独立 phase gates；定义 snapshot 输入闭包、hash/fingerprint、生命周期、无下游重读和失效行为；增加 TOCTOU fixture、A1 保护矩阵、canonical byte/hash 比较；限定 A4 文件/入口；把 A5 改为 selector/fixture 级断言；固定 A6 命令、run_id、报告路径和 warning delta；明确未测量性能与外部环境均不得宣称通过。

## Round 2 — Codex

**Material Issues**

- A6 命令与 source path 没有指明正确工作目录，无法按计划复现；browser smoke 参数也不够明确。
- snapshot 组装期间仍缺少 read/hash/parse 一致性和变化处理；fingerprint 的输入、算法、时间戳/重命名规则未完全固定。
- `WB-WORKSPACE-409` 不是现有契约错误码，可能造成契约漂移。
- “无下游磁盘重读”缺少可观察的读取计数 seam。

### Claude's response

已接受并修订：所有 A6 命令改为带绝对工作目录和完整参数；browser smoke 明确 ZIP、`-Browser failure` 与前置 success smoke；snapshot 从同一稳定 bytes 计算 hash/解析，组装后重枚举并最多重试一次；fingerprint 固定为规范化相对路径与 bytes hash 列表，不含时间戳；并发变化统一复用既有 `WB-CAS-409`，不新增错误码；A2 增加 `WorkspaceReadProbe` 读取计数验收。

## Round 3 — Independent read-only reviewer

**Material Issues**

- A2 输入指纹覆盖不足：现有实现主要聚合 authoring 文档，source registry 另算，audit/ledger 只记录数量，schema 变化未纳入统一身份。修复：把 authoring、sources、registries、audit、ledger、schemas 的规范化路径与字节 SHA 纳入完整闭包指纹，并在复用/输出边界核验。
- A2 读写竞态：`ReadAuthoring` 与 source registry 读取先取文本、再对磁盘文件另算 hash，可能把旧内容与新 hash 组合。修复：同一字节缓冲完成解析与 hash，并增加读后变化检测、重试和并发修改测试。
- A2 生命周期不明确：当前 `Validate`、`Compile`、`Preview` 各自构建 snapshot；需要明确 snapshot 是单次 public 调用的 operation context，跨请求复用必须禁止或使用完整指纹绑定的不透明句柄。本批选择禁止跨请求/全局可变缓存。
- A3 范围过宽：模板构造、content graph、preview 和 ValidationServices 不应在一个子批次同时拆分。修复：改为 A3.x，每批限定入口/方法集合、变更预算和停止条件。
- A4 wire 契约表述有歧义：应比较 CLI/Web 的语义字段、错误码和安全消息，但明确保留 CLI snake_case 与 Web camelCase、退出码和 HTTP 状态的差异。
- A4 Launcher 契约冻结不足：需冻结 `StudioBootstrap` 字段、protocol、固定 loopback port、health、shutdown proof、mutex、job/process cleanup、环境变量和 browser fallback 后再整理。
- A5 UI 与 AI/CAS 混批：修复为 A5-UI 与 A5-AI/CAS 两个独立批次，分别冻结 DOM/文案与 request/response、hash/revision/session/consent/nonce/buffer/CAS 断言。
- 验收证据不足：不只写 F01–F70；需保留 A1 `72/72` 与保护矩阵，逐批固定 named test、fixture、预期错误码/hash、源码/包指纹和证据等级。

### Plan revision

已接受以上意见并重写计划：A2 明确为单次 `WorldbookApplicationService` public 调用的 operation context，补全输入闭包与同一 bytes 的 hash/parse 规则；A3、A4、A5 拆为可独立验收的子批次；A4 明确 CLI/Web wire casing 与 Launcher 协议冻结；A6 与每批验收增加源码/包指纹、证据等级和 A1 保护矩阵要求。下一轮只读审查通过前不写 A2 代码。

## Round 4 — Codex final read-only review

**Material Issues**

- A1 `72/72` 保护矩阵仍只有计划性文字，没有指定受版本控制的矩阵文件和机检行数。修复：新增 `tests/fixtures/a1-protection-matrix.v1.json`，恰好 72 个唯一行，逐行绑定 named test、fixture、expected artifact、比较方式和契约域，测试机检 `72/72`，禁止从当前测试代码自动生成。
- 完整输入闭包仍缺实际读取集合证明。修复：为 `WorkspaceService`、schema validator、registry/source/audit/ledger loader 加 instrumented read-inventory seam，声明闭包与实际读取集合必须相等，并记录 closure manifest/hash。
- `WorkspaceReadProbe` 仍缺每个 operation/path/category 的精确上限。修复：无竞态时 `Validate` 每输入最多 2 次，`Compile`/`Preview`/`Export` 每输入最多 3 次；快照交付后的下游为 0 次；一次重建按同样上限重计，第二次变化为 `WB-CAS-409`。
- 输出边界还需明确防止半发布。修复：derived output 先写随机 staging，snapshot fingerprint 复核后原子发布；边界变化丢弃 staging、不替换旧 compiled/export/current pointer；提交后做 manifest/hash 复核并验证旧 pointer 不变。
- A6 证据仍需固定 schema、命名和独立 release-check。修复：增加 `worldbook-studio-evidence-manifest.v1.schema.json`，固定 UTC `run_id`、文件名、必填字段、command cwd/退出码/stdout/stderr/hash、warning baseline/delta、source/package 指纹，并显式运行 `.\scripts\release-check.ps1 -Package artifacts\WorldbookStudio`。

### Plan revision

已接受以上意见并补入计划的 Phase gates 与 Acceptance contract。下一轮只读审查若确认无 material issue，才创建 A2 子计划并开始实现；当前仍未修改实现代码。

## Round 5 — Codex final read-only review

**Verdict**

已复核最新计划、历史审查意见、Worldbook Studio 当前源码与测试布局。确认：operation-scoped `ValidatedSnapshot`、完整实际输入闭包与 instrumented inventory、同一 bytes 的 hash/parse、最多一次重建与既有 `WB-CAS-409`、三阶段 TOCTOU、精确读数上限、staging/atomic publish/旧 pointer 保护、受版本控制的 72 行 A1 矩阵、独立 evidence manifest、A3.x/A4/A5 子批次边界和显式独立 release-check 均已写入计划；未发现 material remaining gap。

VERDICT: APPROVED
