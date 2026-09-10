# Plan Review Log: PersonaWorkbench AI 生成链路优化
Act 1 (grill) complete — plan locked with the user's explicit autonomous implementation request. MAX_ROUNDS=5.

## Round 1 — Codex**审查结论**

- **预算决策与实现不一致**：计划规定短/长 DSL 为 `500/700`、扩充为 `700/1000`，但现有 `ProviderDraftClient` 仍固定 `DraftMaximumTokens = 4096`，且请求没有按输入长度分级；一行修复：集中加入 `SelectOutputBudget(route, sourceByteCount)`，并将结果写入 `max_tokens`。
- **`think` 兼容性没有验证门**：计划只说“真实响应验证”，但没有定义 Ollama 忽略 `thinking`、返回 reasoning 字段或超预算时的判定；一行修复：增加 Ollama fixture/集成门，断言请求参数、响应结构、实际输出字节与降级错误码。
- **直接自由文本入口仍未被计划具体锁定**：现有 UI 关键主操作仍是 `/api/provider/expand-description`，计划没有明确新入口的 DOM ID、请求 DTO、服务方法和成功后的草稿落点；一行修复：在计划中列出“按钮→端点→`GenerateAsync`/转换→独立草稿状态”的完整调用链并加端到端测试。
- **直接路径可能错误复用扩充语义**：`ConvertThroughProviderDraftAsync` 将 `SourceText` 填入 `ProviderDraftActionRequest.Prompt`，但没有证明 fallback 使用的是稀疏候选契约，而不是旧完整 Persona 草稿契约；一行修复：禁止 DSL 路由 fallback 到旧 draft 路径，统一调用显式 intermediate-candidate provider。
- **稀疏候选的证据来源存在可绕过边界**：解析器允许 `evidenceSourceText` 覆盖 `sourceText`；计划要求“无证据字段拒绝”，但没有规定该参数只能由服务内部传入、不能由请求体控制；一行修复：移除公共解析入口的可选证据源，或强制服务传入并断言它等于原始用户文本。
- **嵌套 `reaction`/`commitment` 仍缺少对象/null/类型矩阵**：已有解析器只展示字段集合校验；计划没有明确 `null`、空对象、字符串替代对象、数组、重复键和未知键的逐项结果；一行修复：为每种 JSON 类型和重复/未知字段定义稳定错误码并加入参数化测试。
- **重复 JSON 属性可能未被拒绝**：`System.Text.Json` 默认允许同名属性，计划只提到“重复索引”，没有覆盖同名 JSON key；一行修复：解析前用 `Utf8JsonReader` 检测重复属性名，或启用等效的重复键拒绝逻辑。
- **“重复索引”没有明确跨数组/跨字段的唯一性范围**：计划没有说明 `axes` 内重复、`flags` 内重复以及 axis/flag 交叉重复是否分别拒绝；一行修复：定义全局索引命名空间和每个数组的唯一性规则，并测试所有组合。
- **证据约束过于脆弱且未纳入计划**：现有实现以文本包含/顺序抽取类规则验证证据，容易被模型改写、大小写/标点/中文变体绕过或误拒；一行修复：候选契约要求返回原文短引文及字符范围/规范化匹配结果，程序按范围验证。
- **事实、推测、建议区仍没有可执行承载契约**：计划承认数据模型可能无法承载置信度和建议区，却仍声称“高置信度推测进入草稿、低置信度进入建议区”；一行修复：本轮明确只允许事实字段进入草稿，推测/建议一律放独立响应字段并禁止写入 `PersonaDocument`。
- **空结构错误没有完整 UI 行为定义**：计划要求返回明确结构为空错误，但没有规定 HTTP 状态、错误码、是否保留旧表单、按钮状态和用户可见提示；一行修复：固定 `422 + persona.intermediate_candidate_empty_structure`，并测试旧草稿完全不变。
- **失败不覆盖现有表单缺少回归门**：计划提到语义，但现有测试检索结果主要覆盖 expansion 隔离，未见直接生成失败后所有 Persona 字段保持原值的端到端断言；一行修复：先填充完整表单，再注入空/非法/超限候选，断言草稿、DSL、扩充文本均不变。
- **生成与转换的并发边界未重新审查**：服务对 Provider 操作使用单一 in-flight lease；直接生成后用户立即转换可能被视为冲突，但计划没有规定 UI 是否等待、取消或允许独立操作；一行修复：为四条真实路由定义并测试状态机：空闲、生成中、转换中、取消、冷却。
- **UI 授权边界只覆盖服务端请求头，不覆盖敏感数据生命周期**：端点检查 session/CSRF，但计划没有验证 API key 不进入前端状态、日志、quarantine、浏览器历史或错误响应；一行修复：增加敏感字段不落盘/不回显/不写日志的测试，并对失败响应做脱敏断言。
- **本地 Provider 的 SSRF/解析时序风险未纳入新路由验证**：现有 endpoint policy 对云端 HTTPS 放行、再做 DNS 地址限制；计划只写“不改变安全确认”，没有证明新直接入口复用同一解析与重定向拒绝链；一行修复：为直接生成和 DSL 转换分别加入私网解析、DNS 变化、重定向和非 loopback HTTP 的拒绝测试。
- **验证门过于偏发布流程，缺少契约一致性门**：计划第 6 步要求构建包和四条真实路由，但没有要求提示词字段、DTO、解析器字段、UI payload、canonical DSL 输出逐项比对；一行修复：增加固定 golden candidate，验证 prompt schema→JSON→parser→document→DSL 全链路字段守恒。
- **没有定义“同语言输出”的可验证标准**：提示词要求同语言，但测试计划没有中文输入、英文输入、混合输入及模型返回另一语言时的拒绝条件；一行修复：增加语言 fixture，并要求程序以原文语言检测结果拒绝明显跨语言文本。
- **没有测试提示词注入/自由文本中的伪 JSON**：直接把用户自由文本送入模型，计划只攻击结构契约，未要求模型忽略文本中的“输出完整 DSL/覆盖规则”等指令；一行修复：加入包含伪系统指令、JSON、Markdown fence 的输入，断言只接受符合 schema 且有原文证据的候选。
- **计划没有定义候选过长与截断的组合行为**：解析器和 Provider client 各有大小限制，但没有规定先截断、响应 `done=false`、JSON 被截断时的统一错误和 usage 保留；一行修复：固定“禁止本地截断，任何不完整 JSON 返回 response-invalid”，并测试 usage/quarantine 元数据。
- **“不自动批准/保存”缺少浏览器级验证**：计划虽写明不自动保存，但没有验证成功生成不会调用 `/api/documents/save` 或 `/approve`；一行修复：在 browser smoke 中拦截请求并断言直接生成成功只更新编辑态，不触发保存/批准。
- **测试计划没有覆盖云端确认与本地 loopback 的权限差异**：计划要求保持云端确认，但没有直接入口分别验证 loopback 无确认、HTTPS 未确认拒绝、确认后仅允许同一 canonical endpoint；一行修复：为每条 Provider 路由补齐三态授权矩阵测试。

**阻断项**

至少预算未落地、直接入口调用链未锁定、证据源可绕过、空结构/失败不覆盖缺少端到端门、敏感凭据生命周期未验证；这些会使“计划完成”无法按工作区的入口→调用→结算→可观察结果标准验收。

VERDICT: REVISE
### Claude's response
Revised the plan to lock the direct UI→`convert-to-dsl`→sparse candidate→parser→canonical DSL chain, remove the default legacy-draft fallback, make the current data model's fact-only boundary explicit, and add concrete gates for budgets, low-thinking parameters, evidence source, duplicate keys/indexes, failure non-overwrite, authorization, and no-save/no-approve behavior. The review's proposed character-range evidence protocol was not adopted because it expands the candidate contract beyond the smallest root-cause fix; the current ordered source extraction remains the bounded evidence rule for this iteration.

## Round 2 — Codex
已解决的上轮阻断项与剩余材料问题见本轮审查输出。

### Claude's response
本轮明确了所有剩余实现点：删除 `ConvertThroughProviderDraftAsync` 和其调用；移除 `evidenceSourceText` 参数；UTF-8 `<=4096`/`>4096` 字节预算阈值；仅 loopback:11434 发送 Ollama `think: low`；字段证据不匹配统一丢弃、结构错误统一拒绝；请求前保存并在失败时恢复核心/扩充/DSL 快照；并将旧 fallback 测试迁移为显式 legacy endpoint 测试。

## Round 3 — Codex
已解决的上轮阻断项与剩余材料问题见本轮审查输出。

### Claude's response
本轮进一步封死实现边界：旧 `/api/provider/generate-draft` 与稀疏 `convert-to-dsl` 保留在同一服务但通过依赖与调用图测试隔离，不再存在隐式 fallback；输入上限固定为扩充 16 KiB、DSL 24 KiB，并测试 4096/4097 边界；新增 `ProviderProtocol` 明确 Ollama 与其他 OpenAI-compatible 本机服务；前端请求代次令牌阻止过期响应恢复/覆盖当前编辑。

## Round 4 — Codex
VERDICT: APPROVED

### Claude's response
Plan converged after four read-only rounds. Implementation may begin: the approved boundaries are direct sparse conversion, no legacy fallback, explicit Ollama protocol, deterministic byte budgets, generation-token UI protection, and fact-only evidence-backed output.
