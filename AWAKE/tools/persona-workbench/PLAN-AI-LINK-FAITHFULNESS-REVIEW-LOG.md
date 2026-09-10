# Plan Review Log: PersonaWorkbench AI 链路证据保真与预算可观测性
Act 1 (grill) complete — plan locked with the user's approval of the narrowed evidence-driven scope. MAX_ROUNDS=5.

## Round 1 — Codex

Findings:
- `num_ctx` 分级缺少具体值、完整 prompt/envelope/output 预算和 4096/4097 边界；UTF-8 bytes 不能直接当 token。
- `completionReason` 已在共享 reader 中读取，但没有贯通到扩充/DSL action response，失败路径会丢失完成原因。
- 当前扩充 PASS 只验证首个引号锚点，固定短样本实际出现了原文未给出的动机、结果和权力关系推断；HTTP 200 不是语义质量证明。
- 没有逐项保护身份、公开/私下、流言模态、压力反应、关系/承诺和独立外观语义的覆盖矩阵。
- 直接稀疏转换和本地 DSL 已完成，本轮不应重复建设。
- stale 失败/取消/云端 Key 子流程和扩充/DSL 的协议参数矩阵测试不足。
- 现有最终包报告缺 direct routes、completion reason、实际 `num_ctx`、prompt hash 和 Worker 同包证据。
- PowerShell 排序一致性可能影响发布绑定复现，但属于本轮范围外的既有验证问题。

VERDICT: REVISE

### Claude's response

Accepted the material AI-chain findings. The plan now fixes expansion context budgets at `8192` for `<=4096` UTF-8 input bytes and `16384` for `4097..16384`, explicitly separates bytes from tokens, and adds boundary/envelope/output tests. It also requires nullable completion reason and envelope kind to flow from the existing reader through both provider action responses and the report, including truncated/empty/invalid/missing cases while preserving the legacy draft response contract.

The plan now defines a semantic coverage matrix for facts, modality, public/private behavior, pressure reactions, relationships/commitments, and non-repeated appearance; HTTP success is no longer accepted as a semantic-quality verdict. It adds stale failure/cancel/key-subflow and protocol-matrix regression tests, plus direct short/long DSL and prompt/request metadata in the same BuildId evidence package. The direct sparse conversion and local DSL path remain unchanged.

The PowerShell sorting observation is recorded as out of scope because it is unrelated to the AI-chain objective and the current `pwsh` binding gate passes; it will not be silently changed in this plan.

## Round 2 — Codex

Findings:
- `num_ctx` 仍缺完整请求预算、余量和资源/延迟门。
- 完成原因未定义 shape-invalid、HTTP/429/redirect/timeout/cancel 等全失败语义，且 legacy 响应兼容只有口头保证。
- 语义矩阵维度仍未固定 source span、规则、人工阈值和冲突处理；当前 runner 仍把 HTTP 200+非空当作 PASS。
- 同 BuildId 证据尚未定义 Worker 绑定、prompt/request 元数据、脱敏和校验 schema。
- BrowserSmoke/stale failure/key 子流程尚未成为硬验收门。

VERDICT: REVISE

### Claude's response

Accepted all P1 findings. The plan now defines a conservative request-byte estimate (`ceil(requestBytes / 2)`), adds `num_predict + 1024` headroom, selects `8192/16384/32768` deterministically, rejects over-budget requests before transport, caps the request below the model's reported `131072` context length, and records duration/resource observations with an explicit `IN_DOUBT` performance result when evidence is unavailable.

Completion metadata now has an explicit full failure matrix and must be parsed from a valid outer envelope even when message/choices content is malformed; legacy draft responses receive a JSON/HTTP snapshot regression and cannot inherit the new fields. The acceptance contract separates `transportVerdict` from `qualityVerdict`; the semantic matrix uses fixed source atoms with `must-preserve`, `modal-preserve`, `must-not-invent`, and `compressible` labels, deterministic automated checks, and a manual rubric with disagreement mapped to `IN_DOUBT`.

The plan now makes BrowserSmoke and the evidence combiner hard acceptance gates, requiring the Worker report hash, BuildId/manifest hashes, prompt hash, redacted request metadata, direct/expand/convert stages, completion metadata, and cleanup state in the combined evidence schema. No second direct conversion or DSL rendering path is introduced.

## Round 3 — Codex

Findings:
- `ceil(requestBytes/2)` is not a provably safe tokenizer upper bound and must not be used as a context sufficiency guarantee.
- Performance was only described as an observation/`IN_DOUBT`; the overall acceptance did not require a performance verdict, baseline identity, or resource threshold.
- `envelopeKind` had ambiguous null/unknown semantics and the acceptance did not require both metadata fields on every stage.
- BrowserSmoke/evidence validator were called hard gates in the response but not explicitly listed with commands and failure rules in Acceptance.
- The semantic matrix lacked a revision/hash and per-atom results in the combined evidence.

VERDICT: REVISE

### Claude's response

Accepted the remaining contract issues. The plan now removes the false tokenizer-upper-bound claim and uses a deterministic request-byte table (`<=12288→8192`, `12289..24576→16384`, `24577..32768→32768`) only for resource control; actual sufficiency is judged by completion reason, Usage and accepted output. It defines `completionReason=null` and `envelopeKind` as a non-null enum (`ollama_native`, `openai_compatible`, `unknown`) with deterministic failure behavior.

The acceptance now has mandatory `transportVerdict`, `qualityVerdict`, and `performanceVerdict`, with a fixed same-model/fixture/serial baseline, `<=2x` latency and `<=1.5x` available `/api/ps` memory gates; missing resource evidence is `IN_DOUBT`. `BrowserSmoke` and `tools/validate-ai-link-evidence.ps1` are explicit hard gates with nonzero/IN_DOUBT/hash-mismatch failure rules. The semantic matrix has revision/hash and per-atom automated/manual/conflict results, and prompt/request metadata hashing plus redaction rules are explicit.

## Round 4 — Codex

Findings:
- The completion-reason rule still used “尽量保留” and Acceptance did not require `envelopeKind` on every stage.
- Budget tests did not explicitly cover complete request-byte boundaries `12288/12289`, `24576/24577`, and `32768/32769`.
- Acceptance named `tools/validate-ai-link-evidence.ps1`, but the plan did not yet create it or define its parameters, schema checks, output, and exit code.

VERDICT: REVISE

### Claude's response

Accepted all three concrete findings. The plan now provides a deterministic outer-envelope/content-shape mapping for `completionReason` and `envelopeKind`, requires both fields on every stage, requires exact complete-request-byte boundary fixtures, and adds an explicit implementation step for `tools/validate-ai-link-evidence.ps1` with parameters, schema/hash/verdict checks, cleanup checks, and exit code 0/1 behavior. The BrowserSmoke and validator commands are now written verbatim in Acceptance.

## Round 5 — Codex

Findings:
- The hash hard gate cannot independently recompute `BuildId/manifest/Worker/prompt/matrix` hashes when the validator only reads self-reported evidence and has no immutable source-input/path contract; a report could self-report matching hashes.
- The budget hard gate requires complete `system prompt + serialized envelope` byte accounting, but the schema did not yet require `requestBytes`, `systemPromptBytes`, canonical serialization rules, or a request-body hash that the validator can recompute.

VERDICT: REVISE

### Claude's response

These findings are material to the evidence hardening layer. The recommended resolution is to add immutable validator inputs and explicit request accounting before implementation: the validator must receive the candidate package/source root, Worker report, semantic matrix, and baseline report through fixed read-only paths; recompute package/source/Worker/prompt/matrix hashes from those inputs; and reject self-reported-only evidence. The evidence schema must require `systemPromptBytes`, `envelopeBytes`, `requestBytes`, a canonical UTF-8 serialization rule, and a request-body hash, with the validator recomputing the byte total and selected `num_ctx`.

The five-round review cap has been reached without `VERDICT: APPROVED`. No production or test code is being changed until the user signs off on this remaining evidence-contract resolution or explicitly narrows the hard-gate scope in a new review batch.
