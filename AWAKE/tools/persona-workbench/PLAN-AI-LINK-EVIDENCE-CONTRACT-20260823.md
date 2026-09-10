# Plan: PersonaWorkbench AI 链路证据独立复算契约
_Locked via grill — user approved the recommended evidence-hardening follow-up_

## Goal
让 PersonaWorkbench AI 链路的候选/基线证据不能仅凭报告自报哈希通过。建立一个只读、可重复的 evidence schema v2 和 validator：从固定源码、发布包、Worker 报告、语义矩阵与临时请求捕获中重新计算 BuildId、manifest、prompt、matrix、输入/请求字节和请求体 hash，并明确失败/不确定退出规则。该计划不改变 Persona 业务模型，也不调用云端 Provider。

## Approach
1. 固定 `tools/persona-workbench-ai-link-evidence.v2.schema.json`：顶层 `schemaVersion`、`candidate`、`baseline`、`model`、`fixtures`、`workerDiagnostic`、`matrix`、`workbenchStages`、`verdicts`、`cleanup` 为必填，`additionalProperties=false`；stage 只允许四个真实 Workbench 阶段 `expand-short`、`convert-short`、`expand-long`、`convert-long`，Worker 诊断单独存放，不混入 Workbench 阶段。每个 stage 必须提供输入 hash/bytes、`systemPromptBytes`、`envelopeBytes`、`requestBytes`、实际发送 request body hash、选定 `num_ctx/num_predict`、completion reason、envelope kind、Usage、duration、transport/quality/performance verdict 和 accepted/failure 状态。未知字段、缺字段、未知枚举、重复 JSON key 或阶段重复均失败。报告不保存 API Key、完整原文或完整响应。
2. 固定 validator 输入契约：`-WorkspaceRoot`、`-SourceRoot`、`-PackagePath`、`-WorkerReportPath`、`-MatrixPath`、`-EvidencePath`、`-BaselineSourceRoot`、`-BaselinePackagePath`、`-BaselineWorkerReportPath`、`-BaselineMatrixPath`、`-BaselinePath`、`-RequestCapturePath` 和 `-BaselineRequestCapturePath`。所有路径必须是绝对路径、存在且位于 `WorkspaceRoot` 下；validator 只读并拒绝从 evidence JSON 内部自带路径取权威输入。candidate 与 baseline 都必须是 v2 证据并分别提供完整 capture。跨 candidate/baseline 的比较规则固定为：schema/stage 顺序、模型名/digest、fixture/input hash/bytes 和 `envelopeKind` 枚举合法性为 exact；每个请求自己的 prompt/request hash、`systemPromptBytes`、`envelopeBytes`、`requestBytes`、`num_ctx`、`num_predict`、completion reason、Usage 与 duration 先按自身 capture/evidence 一致性校验，candidate duration 对同类 baseline 阶段不得超过 2 倍；prompt/hash、预算、completion reason、Usage 的差异只记录为优化结果，不要求相等。缺失 baseline 证据为 `IN_DOUBT`，不得默认为通过。
3. 固定临时 request capture 格式：Provider 客户端在 `HttpClient.SendAsync` 前，将已经物化、实际发送的 UTF-8 body 交给注入的诊断 capture writer；每个阶段一条 JSONL，保存 `stage`、`operation`、`protocol`、`endpointClass`、`model`、fixture/input hash、无 Authorization 的安全 headers、body Base64 和 body byte count。仅当 `PWB_REQUEST_CAPTURE_PATH` 非空且 loopback 请求携带受保护的 `X-Pwb-Diagnostic-Stage` 时，Workbench 在 ActionService 建立 `ProviderRequestCaptureScope`；scope 使用 `AsyncLocal` 从 ActionService 流入 Provider client，并在 finally 中释放。stage 只能是四个固定 ID，capture writer 按文件锁串行写入；每个 stage 恰好一条记录，缺失、重复、未知 stage 或并发写入均使 validator 返回 `1`。capture 不再记录 Workbench 外层请求。capture 只用于本次 validator 运行，validator 纯读；外层 wrapper 在 validator 返回后负责删除 capture、停止本次启动的进程并检查端口，最终汇总 cleanup。
4. 固定 hash/序列化规则：所有文本按 UTF-8、无 BOM、SHA-256、大写十六进制；请求体 hash 对 capture 中的实际发送字节计算；`systemPromptBytes` 对解析出的唯一 `messages[*].content`（role=`system`）字符串 UTF-8 计算，`envelopeBytes` 对解析出的唯一 `messages[*].content`（role=`user`）字符串 UTF-8 计算，`requestBytes` 对实际发送 body 字节计算；prompt hash 对 system prompt UTF-8 计算；矩阵 hash 对 MatrixPath 原始文件字节计算；Worker hash 对 WorkerReportPath 原始文件字节计算。manifest 校验复用 `tools\write-source-manifest.ps1` 的文件选择、排序、行格式和 UTF-8 无 BOM 算法，并复核 package manifest 每个条目的实际文件 hash、路径覆盖和 `PACKAGE-MANIFEST.sha256.txt` 排序，不信任报告自报值。BuildId 不声称可由输入重新生成：validator 读取 package `BUILD-ID.txt`，验证格式、末 12 位与重算 source hash 绑定，并要求 evidence/candidate/baseline/报告一致。
5. 创建 `tools/validate-ai-link-evidence.ps1`、`tools/persona-workbench-ai-link-evidence.v2.schema.json` 和 focused tests：参数为上述十三项路径；validator 使用严格 JSON 解析/Schema 校验并额外拒绝重复 key，检查所有必填字段、类型、枚举、阶段顺序和路径边界。退出码固定为：`0`=证据 schema、路径、所有重算 hash/字节/预算字段、四阶段、三 verdict 和 candidate/baseline 比较全部通过；`1`=证据失败或不确定，包括阶段 FAIL/IN_DOUBT、hash/字节/参数不一致、报告缺字段或 baseline 不足；`2`=命令参数、路径、JSON/schema/capture 格式非法。validator 本体只读，不负责删除 capture、停止进程或释放端口；顶层 `try/catch` 保证输入/格式错误优先为 `2`，否则证据失败为 `1`。外层 wrapper 在 validator 返回 `0` 后执行 cleanup，任一 cleanup 失败将最终退出码强制映射为 `1`。测试伪造单字段、重复 key、篡改 capture、篡改 matrix、篡改 package manifest、缺失阶段、未知 envelope kind、不一致 `num_ctx`、baseline 缺 capture 和 cleanup 失败，均必须失败。
6. 增加仅诊断启用的 Provider request capture hook：由 `PWB_REQUEST_CAPTURE_PATH` 环境变量控制，默认关闭；`X-Pwb-Diagnostic-Stage` 只接受 loopback、有效 session、有效 CSRF 和已开启 capture 的请求，其他情况按普通请求忽略或拒绝，不把 stage 写入业务模型。Provider 客户端在发送前捕获最终 UTF-8 body，runner 通过四个固定 stage ID 绑定 capture。capture writer 不记录 Authorization/API Key，写入失败不影响业务响应但会使 evidence validator 返回 `1`。candidate runner 的四个 stage、Worker、报告和 evidence 使用同一 candidate BuildId；baseline runner 的对应内容使用独立 baseline BuildId，validator 只校验各自内部绑定，不要求 candidate 与 baseline BuildId 相等。两者必须使用同模型/fixture 的 v2 evidence；Worker 原始报告保持不可变，evidence combiner 只记录其 hash、模型/夹具一致性和来源路径，不修改 Worker 内容。combiner 先运行纯读 validator，再执行 cleanup；只有 validator 返回 `0` 且 cleanup 全部成功才允许把证据标记为 PASS。
7. 在后续 AI 链路实现中复用该证据契约：先运行 validator focused tests，再运行 Worker-low、Workbench direct/expand/convert 和 BrowserSmoke；证据校验失败不得进入业务优化结论。wrapper 的固定顺序为：启动并记录本次进程 → 串行运行四阶段并写 candidate/baseline capture → 运行纯读 validator → 删除两份 capture → 停止仅由本次启动且路径匹配的进程 → 验证端口释放 → 写最终 cleanup。任一删除、停止或端口检查失败都将最终退出码映射为 `1`，不得覆盖为 PASS。

## Key decisions & tradeoffs
- 最终报告不保存原文/完整响应，但运行期间允许固定工作区内的临时 request capture 作为独立复算输入；validator 在 cleanup 前只读 capture，wrapper 在验证后删除，清理失败时最终证据不能判 PASS。
- validator 的权威来源是命令行参数指向的只读文件，不是 evidence JSON 内部自报路径或 hash；candidate 与 baseline 各自有独立 source/package/Worker/matrix/capture 输入。
- prompt hash 由实际请求 body 中解析出的 system prompt 计算，避免依赖 PowerShell 解析 C# 字符串；BuildId 由 package metadata 读取并校验 source hash 绑定，不声称可从输入重生成。
- request body hash 与字节统计以实际发送 UTF-8 body 为准，禁止用重新序列化后的近似值代替；capture stage 通过受保护 header + `AsyncLocal` scope 显式绑定。
- Worker 诊断与 Workbench 验收分层；Worker 报告不被改写，只通过 hash 和模型/夹具指纹绑定。

## Risks / open questions
- 临时 capture 包含固定测试输入和本地模型请求内容，必须限制在工作区临时目录并在 validator 后由 wrapper 删除；cleanup 对象必须记录 `captureDeletionAttempted`、`captureDeleted`、`processStopped`、`portFree` 和 `error`，清理失败时最终证据不能判 PASS。
- 已有发布 manifest 脚本可能输出旧格式；validator 必须复用其权威算法或明确解析同一格式，不另造第二套 manifest 规则。
- PowerShell 5.1 与 `pwsh` 的 JSON/排序行为可能不同；validator focused tests 必须在实际交付使用的 `pwsh` 入口执行，并记录版本。

## Out of scope
- 不修改 Provider prompt、预算、解析器、PersonaDocument、DSL、保存批准、云端配置或 Bannerlord；仅增加默认关闭的诊断 capture hook 和 stage scope，不改变业务响应。
- 不调用 Ollama/Worker/云端 Provider；本计划只验证证据文件、请求捕获和默认关闭的诊断 hook。
- 不修改历史报告；旧报告只能作为 baseline 输入，不能被回写。

## Acceptance
- `tools/validate-ai-link-evidence.ps1` 与 `tools/persona-workbench-ai-link-evidence.v2.schema.json` 存在，validator 参数、四个 stage ID、退出码 `0/1/2` 和错误输出符合本计划。
- validator 能从命令行输入重新计算并校验 BuildId 绑定、source/package manifest、Worker/matrix/prompt/request hash、所有字节字段、四阶段的 `num_ctx/num_predict` 与 baseline 比较；BuildId 按 metadata 读取并校验，不声称由输入重新生成。
- 任一 evidence 自报字段、capture、matrix、Worker 报告或 package manifest 被篡改时，validator 返回退出码 `1`；参数/路径/schema/capture 非法返回退出码 `2`；capture 删除或端口清理失败由 wrapper 返回最终退出码 `1`。
- focused tests 在 `pwsh` 下通过，覆盖 schema/重复 key、四阶段、candidate/baseline 独立输入、manifest/矩阵/报告篡改、预算字段、AsyncLocal stage 绑定和 cleanup 顺序；通过 validator 且 cleanup 完成的 evidence 才能被后续 AI 链路报告引用，本计划不宣称模型质量或 Workbench 功能通过。
- 通过 validator 的 evidence 才能被后续 AI 链路报告引用；本计划不宣称模型质量或 Workbench 功能通过。
