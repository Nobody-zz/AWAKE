# Plan: PersonaWorkbench AI 链路证据保真与预算可观测性
_Locked via grill — based on the user's approval of the evidence-driven optimization direction_

## Goal
在不改变 Persona 数据模型、保存/批准协议、本机与云端安全边界的前提下，降低 PersonaWorkbench AI 生成链路的上下文预算错配和不可解释失败，并减少可编辑扩充对原文事实、流言语气和行为动机的无依据强化。直接识别仍保持一次稀疏 Candidate 调用；canonical DSL 继续由本地代码生成；截断或不完整 JSON 继续不提交。

## Approach
1. 先固定现有代码与本机 Ollama `gpt-oss:20b` 的基线证据：短/长扩充、短/长直接 DSL、Worker-low 对照、请求输入字节、输出字节、Usage、完成原因、耗时和最终接受状态；不把 Worker 结果当作 Workbench 验收。
2. 增加 Provider 响应完成原因的只读诊断字段：复用现有 `ProviderChatResponseReader`，将 `completionReason`（无 Provider 外层或无 reason 时为 `null`）和 `envelopeKind`（固定枚举 `ollama_native`、`openai_compatible`、`unknown`，不使用 null）从扩充、稀疏 DSL 客户端贯通到 ActionResponse 和测试报告；成功、`length` 截断、空内容、结构错误和字段缺失都按确定性矩阵保留可观察值，但不写入 `PersonaDocument`、文件或批准状态；旧 legacy draft API 保持原响应契约。
3. 将原生 Ollama 扩充的固定 `num_ctx=8192` 改为固定请求字节档位：按 UTF-8 `system prompt + serialized envelope` 字节数选择 `<=12288 → 8192`、`12289..24576 → 16384`、`24577..32768 → 32768`；超过 `MaximumEnvelopeBytes` 或档位上限则在本地发请求前返回稳定的上下文预算错误。该表是请求预算/资源控制，不是假装提供 tokenizer 精确 token 上界；实际是否充足只能由响应 `completionReason`、Usage 和 Workbench 接受结果证明。源文本仍拒绝超过既有 `ExpansionMaximumInputBytes`，`num_predict` 仍由现有扩充预算函数选择 `3072/4096`，请求上下文不超过本地模型已报告的 `131072` context length。短/长边界、中文/ASCII、`4096/4097`、最大输入、控制 envelope 和预算拒绝都要有测试；OpenAI-compatible 和云端请求不改变既有参数策略。候选包记录请求字节档位、实际 Usage、耗时和 Ollama `/api/ps` 可用时的内存观察；不通过预估值宣称 token 充足。
4. 收紧扩充系统提示词：明确保留“传言/据说/可能”等原文模态，不把流言写成事实；禁止新增未给出的动机、目的、因果、关系、阵营、身份或结果；只允许在原文已有行为证据时做同义压缩，不要求模型解释未经证据支持的心理含义。保留可编辑 prose 输出、首句锚点、同语言和禁止结构化输出约束。固定夹具建立语义覆盖矩阵，分别检查身份、公开行为、私下行为/流言模态、压力反应、关系/承诺和非重复外观；独立语义消失与重复外观删除分开判定。
5. 增加契约测试和报告字段：完成原因先从已解析的外层 JSON 独立读取，并锁定以下映射：外层 JSON 无法解析、HTTP error、429、redirect、timeout 或 cancel → `envelopeKind=unknown`、`completionReason=null`；可解析的 Ollama envelope → `envelopeKind=ollama_native`，reason 优先取 `message.done_reason`，其次取根 `done_reason`，缺失为 null；可解析的 compatible envelope → `envelopeKind=openai_compatible`，reason 取 `choices[0].finish_reason`，缺失为 null；外层 JSON 可解析但 message/choices/content 形状错误时仍按上述 envelope 判定规则读取可见 reason，读不到则为 null。覆盖 `stop`/`length`/缺失、空内容、缺少 `message`/`choices`、JSON-invalid 和全传输失败矩阵。覆盖 Ollama 原生与 OpenAI-compatible 两种 envelope、扩充和 DSL 两条路径的协议参数矩阵、上下文预算边界、扩充提示词关键禁令、失败时保留旧内容、过期成功/失败/取消不覆盖、云端 Key 设置子流程不污染当前文档、直接 DSL 不触发扩充/保存/批准；不通过脆弱的“模型必须输出某一句”测试冒充语义质量证明。旧 `/api/provider/generate-draft` 增加旧响应 JSON/HTTP 快照回归，新增字段只允许出现在扩充/稀疏 DSL DTO。测试报告额外记录 prompt revision/hash、实际请求参数、completion reason 和 envelope kind。
6. 将验收拆成三个明确结果：`transportVerdict`（HTTP、解析、校验、回填和状态隔离）、`qualityVerdict`（语义覆盖矩阵）与 `performanceVerdict`（固定基线、耗时和资源观察）；任一 verdict 为 `IN_DOUBT` 或 `FAIL`，整体都不得为 `PASS`。基线固定为同模型 digest、同 fixtures、同串行顺序和基线 BuildId；性能门为每个同类阶段耗时不超过基线两倍，且 Ollama `/api/ps` 可用时模型内存指标不超过基线 1.5 倍，指标不可用则 `performanceVerdict=IN_DOUBT`。重新打包后按固定顺序验证：先 Worker-low，再真实 Workbench 的直接短/长 DSL、短/长扩充以及扩充后转换；比较基线与候选的接受率、截断率、完成原因、Usage、耗时和输出长度，并按矩阵检查固定夹具的身份、公开行为、私下行为/流言模态、压力反应、关系/承诺和非重复外观。Worker 与 Workbench 通过一个 evidence combiner 绑定到同一 BuildId/source/package manifest hash；Worker 原始报告保持不可变，合并报告同时包含 Worker 报告 hash、Workbench 阶段、prompt hash、脱敏请求元数据和清理状态。若仅参数已发送而模型实际行为无法从响应证明，报告为“不可证实”，不宣称优化成功。
7. 创建证据校验器 `tools/validate-ai-link-evidence.ps1`：参数为 `-EvidencePath` 和 `-BaselinePath`，校验 evidence schema v2、BuildId/manifest hashes、Worker 报告 hash、prompt/matrix hash、六个 Workbench 阶段、每阶段 `completionReason`+`envelopeKind`、三 verdict、资源/延迟基线和 cleanup；缺字段、hash 不一致、阶段失败或任一 verdict 为 `IN_DOUBT/FAIL` 时输出诊断并以退出码 1 结束，全部通过才退出 0。该校验器只读取报告，不调用 Provider、不启动服务、不修改源文件。

## Key decisions & tradeoffs
- 本轮优化“去重复、保语义”，不把所有输出压成更短，也不增加新的推测/置信度数据模型。
- 直接识别是默认主路径；AI 扩充仍是用户主动触发的可选路径，扩充结果必须经过用户确认后才可进入 DSL 转换。
- 不接受半截 JSON，也不做本地 JSON 修补；截断只记录完成原因和 Usage，并保持当前表单不变。
- `num_ctx` 只在原生 Ollama 路径分级调整；不假设所有 OpenAI-compatible Provider 支持 Ollama 参数。
- 扩充提示词可以要求证据保真，但程序不伪装成能可靠判断全部自然语言幻觉；模型质量必须通过固定夹具实测和明确的人工检查项评估。
- 完成原因是诊断元数据，不进入 Persona DSL，避免把 Provider 方言泄漏到运行时内容契约。
- 包清单的 PowerShell 排序一致性属于既有发布验证问题，不是本轮 AI 链路优化的实现目标；本轮固定使用已经通过的 `pwsh` 验证入口，不顺手改动打包/绑定脚本。

## Risks / open questions
- 不同 Ollama 版本或模型可能忽略 `think`、`num_ctx` 或 `done_reason`；若请求已发送但响应无法证明实际生效，只记录兼容性不确定。
- 更大的上下文可能增加内存占用和首 token 延迟；验收必须同时看耗时和资源风险，不能只看是否成功。
- 提示词收紧可能减少扩充的文采或行为解释；如果固定夹具显示独立语义被过度删除，应回退该提示词改动，而不是继续提高输出上限。
- 长文本 DSL 的 Worker-low 策略拒答不等于 Workbench 失败；两层结果必须分开记录。
- 当前最终包四路由 PASS 只有部分基线证据：既没有 direct-short/direct-long，也没有完整 completion reason、实际 `num_ctx` 和 prompt hash；候选包必须补齐这些字段后才可判定本轮验收。
- 语义覆盖矩阵采用固定 source span/关键词原子：每个原子标记为 `must-preserve`、`modal-preserve`、`must-not-invent` 或 `compressible`；自动检查只负责可重复的锚点、模态词和明确禁词，较宽泛的同义改写由人工按同一 rubric 复核。矩阵文件本身有 revision/hash，合并报告记录每个 atom 的自动结果、人工结果和冲突原因；自动与人工结论不一致时质量为 `IN_DOUBT`，不以单次模型输出强行判定。

## Out of scope
- 不修改 `PersonaDocument`、canonical DSL 字段、保存/批准流程或历史 Persona 文件。
- 不修改全局 Provider 配置、`gpt-5.6-luna` 设置、云端端点或 API Key 生命周期。
- 不删除旧兼容 `/api/provider/generate-draft` 路由，不重构两个大型文件，不新增分块合并或截断半成品提交。
- 不启动 Bannerlord，不把本地 Worker 诊断包装成产品验收，不调用云端 Provider。

## Acceptance
- Core/Web 测试通过，Release build 为 0 warnings / 0 errors。
- Provider 响应完成原因在两种 envelope 中可被测试读取，缺失时为 null，不影响旧客户端解析。
- 原生 Ollama 扩充请求在短/长预算边界发送预期 `num_ctx`；非 Ollama 请求不出现 `think` 或 `options.num_ctx` 的误发送。
- 直接短/长 DSL 与短/长扩充不因本轮改动出现截断、空 Candidate、解析失败或旧表单覆盖；失败时旧内容保持不变。
- 固定夹具报告包含 Worker 与 Workbench 分层结果、输入来源、Usage、完成原因、耗时和清理状态。
- 固定夹具报告包含 BuildId/manifest hashes、Worker 报告 hash、Workbench direct/expand/convert 六个阶段、输入来源、Usage、完成原因、请求预算、prompt hash 和清理状态。
- `transportVerdict`、`qualityVerdict` 与 `performanceVerdict` 都通过；语义矩阵的自动检查和人工 rubric 都通过，且没有不一致项时，才允许整体 `PASS`。
- 人工质量检查至少确认：事实与流言语气未被混淆、未新增固定夹具未提供的动机/因果/身份、独立人格维度没有仅因压缩而消失。
- 旧 `/api/provider/generate-draft` 的 JSON/HTTP 响应快照保持不变；新增诊断字段不泄漏到 legacy response。
- `BrowserSmoke` 与 `tools/validate-ai-link-evidence.ps1` 是硬门：先运行 `dotnet run --project tests/PersonaWorkbench.BrowserSmoke/PersonaWorkbench.BrowserSmoke.csproj -c Release --no-restore -- "C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\tools\persona-workbench"`，再运行 `pwsh -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "tools/validate-ai-link-evidence.ps1" -EvidencePath <candidate-evidence.json> -BaselinePath <baseline-evidence.json>`；任一退出码非 0、任一阶段失败、任一 verdict 为 `IN_DOUBT` 或 BuildId/manifest/prompt/matrix hash 校验失败，整体验收返回非通过。
- 上下文预算测试必须同时构造完整请求字节数恰为 `12288`、`12289`、`24576`、`24577`、`32768` 和 `32769` 的 envelope，分别断言 `8192/16384/32768` 或传输前稳定拒绝；只测试源文本 `4096/4097` 不算覆盖预算档位。
- `promptHash` 为展开后的完整系统提示词字符串按 UTF-8 SHA-256；请求元数据只记录协议、端点类别、模型、输入 hash/bytes、envelope bytes、选定参数和响应元数据，不记录原文、API Key 或完整响应。
