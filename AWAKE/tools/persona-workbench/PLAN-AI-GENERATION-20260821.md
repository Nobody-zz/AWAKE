# Plan: PersonaWorkbench AI 生成链路优化
_Locked via grill — revised after Codex adversarial review; implementation authorized by the user's autonomous “做” request_

## Goal
让用户只填写自由文本即可直接识别并生成可编辑 Persona 草稿与 canonical DSL；扩充仍保留为可选高级步骤。第一阶段只承诺“有原文证据的事实、短文本和有限轴/标签”进入草稿，推测与建议不进入当前 PersonaDocument，避免把尚未有 UI/存储承载的元数据伪装成已实现能力。

## Approach
1. 将现有 `convert-to-dsl` 稀疏候选路径设为默认直识别调用链：`#generate-provider-draft` 改名/改文案为直接识别按钮，前端把 `#provider-prompt` 作为 `SourceText` 调用 `/api/provider/convert-to-dsl`，成功后只调用 `applyEditableDocument` 和 DSL 预览，不调用保存/批准；扩充按钮继续调用 `/api/provider/expand-description`，不互相隐式触发。
2. 禁止 `convert-to-dsl` 在默认生产装配中回退旧的完整 Persona 草稿协议；当稀疏转换客户端不可用时返回稳定的 `provider.intermediate_action_unavailable`，旧 `/api/provider/generate-draft` 仅保留兼容测试/高级调用，不作为直识别隐式 fallback。
3. 删除 `ConvertThroughProviderDraftAsync` 及其默认调用点；`ConvertToDslCoreAsync` 仅接受构造器注入的非空稀疏 `IProviderDslConversionClient`，客户端缺失立即返回 `provider.intermediate_action_unavailable`，并加入源码/服务测试断言该调用图不包含 `GenerateCoreAsync`；旧 `/api/provider/generate-draft` 只调用旧 `IProviderDraftClient`。`LocalDisplayName` 为空时由本地生成默认显示名而不触发旧协议。重写 DSL 转换提示词并以解析器契约为准：完整列出 root、reaction/commitment 对象结构；明确左负右正轴方向、flags 需要的组合证据、同语言短引文、无证据省略/null、禁止伪 JSON/Markdown/DSL。`summary` 等文本字段只能是原文有序抽取，canonical DSL 始终由程序生成。
4. 将 `PersonaIntermediateCandidateParser.Parse` 签名收窄为 `(candidateJson, sourceText, localId, localDisplayName, registry)`，删除 `evidenceSourceText` 参数和所有调用；根对象、reaction/commitment 必须为 object 或 null；空对象允许但不产生字段；字符串/数组/未知键/重复键拒绝；axes 和 flags 各自 index 唯一；越界、缺 source、重复键、重复 index、未知字段、根类型错误统一拒绝候选；合法结构中的单条 axis/flag 证据不匹配统一丢弃该条，不让同一输入在不同调用点产生不同结果；保持整个旧表单不覆盖。
5. 集中实现按 UTF-8 输入字节数的本地预算和 Ollama 参数：阈值固定为 `<=4096` 字节短文本、`>4096` 字节长文本（扩充最大 UTF-8 输入为 16 KiB，DSL 最大 UTF-8 输入为 24 KiB，且加入恰好 4096 与 4097 字节 fixture 断言分支可达）；当前 DSL 使用 700/700，扩充使用 700/1200。请求 DTO 增加 `ProviderProtocol`，UI 在 AI 设置中明确选择 `ollama` 或 `openai_compatible`，默认本机地址选择 `ollama`；仅当 `ProviderProtocol == "ollama"` 且端点为 loopback 时添加 `think: "low"`，并对原生 Ollama 使用稳定 `seed: 42`；其他本机 Provider 不发送该字段，云端保持原有确认/Key/HTTPS 规则。预算选择和请求字段通过单元测试锁定，无法证明服务实际采用 low 时只报告“不兼容”而不伪造成功。
6. 完善错误与 UI 状态：请求开始前缓存核心字段、扩充文本和 DSL 预览，并递增 `providerOperationGeneration`；只有当前代次的 HTTP/Provider 成功且 draft+dsl 非空时更新状态，任何当前代次的失败、取消、冷却或解析错误恢复快照，过期响应不得写回 UI；空结构返回 `provider.persona_structure_empty`；所有 Provider 失败均保留旧核心字段、扩充文本和 DSL；按钮按单一 in-flight lease 禁用，取消/冷却不自动重试；成功直识别不自动保存或批准，浏览器测试拦截并断言无 save/approve 请求。
7. 增加自动化契约测试：旧的 fallback 测试改名为显式 legacy draft endpoint 测试，并新增源码/服务测试证明默认 `convert-to-dsl` 不可达旧 fallback；提示词字段守恒、嵌套对象类型矩阵、重复键/重复索引、原文证据、伪 JSON 注入、预算/think 字段、直接入口调用链、失败不覆盖、授权矩阵和 canonical DSL golden candidate。
8. 构建包后按固定顺序验证：先 `gpt-oss:20b` Worker low 诊断，再 Workbench 的直接短/长识别路由和扩充/转换回归；不并发、不重试、不调用云端、不启动 Bannerlord，记录 Worker 与 Workbench 分层结果。

## Key decisions & tradeoffs
- 直识别是默认主路径，避免简单输入先付出一次扩充调用；扩充仍可用于用户主动要求更丰富文本的场景。
- 本轮只把可验证的原文事实和证据-backed 结构写入 PersonaDocument；推测/建议区暂不实现，避免没有承载契约却声称可编辑推测。
- 直接入口复用现有授权、端点解析和 session/CSRF，不新增绕过安全边界的接口；loopback 无 Key，云端仍需显式确认和会话 Key。
- `reaction`/`commitment` 为嵌套对象；null/省略表示未知，不能用字符串代替对象。重复 JSON 属性、重复数组 index 和未知字段都视为候选错误。
- 证据校验继续以原文短引文和规范化有序匹配为本轮最小改动；字段证据不匹配统一丢弃，结构契约错误统一拒绝；不在本轮引入字符范围协议，避免扩大候选格式和迁移面。
- 预算是输出上限，不保证模型一定使用全部额度；真实验收同时看 finish reason、JSON 完整性、Usage 和 Workbench 结果。

## Risks / open questions
- 本地 Ollama 版本可能忽略 `think: low`；若请求被接受但响应没有可观测标志，报告为“参数已发送、实际思考不可证实”。
- 旧测试可能依赖 `convert-to-dsl` 的 ProviderDraft fallback；本轮必须迁移为显式测试 `/api/provider/generate-draft`，默认直识别不得回退；保留 `ProviderDraftActionService` 但以依赖与调用图测试封死两条路径，不做无关大重构。
- 前端代次令牌只在 `runProviderOperation` 内部维护，不写入网络请求或 Persona 文件。\n- 现有模型的自由文本可能只包含外貌或剧情而无可映射轴；正确结果是结构为空错误或仅保留有证据文本，不自动填满字段。

## Out of scope
- 不实现推测置信度/建议区的新数据模型，不自动批准或保存。
- 不修改全局 `gpt-5.6-luna` 配置、历史日志、旧发布包、Bannerlord 运行时或无关项目。
- 不启动 Bannerlord；游戏内验证仍由用户运行游戏后提供日志。


## Validation — 2026-08-21

- Final package: `artifacts\PersonaWorkbench-FreePreview-r57-20260821`.
- Model: Ollama `gpt-oss:20b`, digest `17052f91a42e97930aa6e28a6c6c06a983e6a58dbb00434885a0cf5313e376f7`, Worker diagnostic uses `low` thinking.
- Workbench direct DSL: short PASS (22.0s, 1121-byte DSL, draft present); long PASS (11.6s, 4094-byte DSL, draft present).
- Optional expansion: short rejected as `provider.expansion_evidence_missing` (14.0s); long rejected as `provider.expansion_truncated` at 1000 completion tokens (83.7s). Both failures preserved the prior editable content.
- Overall paired gate: `FAIL` by the strict rule because optional expansion did not complete; primary free-text direct recognition gate: `PASS`.
- Cleanup: r57 process stopped and port `51337` verified free.

## Validation — r58 to r61 — 2026-08-21

- r58 isolated the first two real failures: short expansion lost its quoted evidence anchor; short direct DSL could return an empty sparse structure; long expansion also remained budget-sensitive. Direct DSL itself passed on both original fixtures.
- r59/r60 testing showed the prompt wording and stable seed alone did not fix expansion. A temporary local tee replay captured the actual request and exposed the root cause: the expansion envelope was JSON-serialized with escaped Unicode, then embedded as a string, so Ollama received literal `\\uXXXX` text instead of Chinese source text. No cloud Provider was used.
- r61 fixed the envelope with `JavaScriptEncoder.UnsafeRelaxedJsonEscaping`, retained the exact first-sentence rule, strengthened the sparse DSL evidence requirement, and sent native Ollama `seed: 42`. Web/Core tests: all passed; build: 0 warnings, 0 errors.
- r61 Worker-low diagnostic: short expansion PASS (11.9s), short DSL PASS (12.7s), long expansion PASS (26.7s), long DSL PASS (12.0s); model `gpt-oss:20b`, digest `17052f91a42e97930aa6e28a6c6c06a983e6a58dbb00434885a0cf5313e376f7`.
- r61 Workbench real routes: short expansion PASS (12.4s, 405-byte output, 439/179/618 usage); short DSL PASS (14.7s, 1019-byte canonical DSL, draft present); long expansion PASS (20.3s, 669-byte output, 1572/275/1847 usage); long DSL PASS (11.0s, 1256-byte canonical DSL, draft present). All four returned HTTP 200 with non-empty accepted outputs and no replacement failure payload.
- r61 package executable SHA-256: `3C8D57D5C8D6A118CC0ACD6175B18226315306973E2FF9586B0F4CE2E157DF8C`. Process stopped and port `51337` verified free.
- Final paired gate: `PASS` for the fixed short/long expansion and DSL regression. Primary direct recognition remains a separate successful path; optional expansion now also completes on both fixtures.
