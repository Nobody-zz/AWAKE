# PLAN：Quick Authoring 上下文预算修复（2026-09-11）

范围：Worldbook Studio AI 生成链路（Quick Authoring）。不含世界书语义迁移、不含 AWAKE 模组本体。

## 0. 一句话结论

Quick Authoring 目前**必然打爆本机 Worker 的上下文窗口**：Pass B 提示词 8839 token > 参考 Worker 的 `num_ctx=8192`；
8192 档下实测整轮失败，32768 档下能通过但只有 8.25 tok/s。**把提示词压进 8192 预算内，同时修正确性、速度和成本**，
这是本项目当前唯一的最大杠杆（实测 Pass A 单轮 337 s → 98.8 s）。

## 1. 决定性实验（同一 fixture、同一模型、同一 Studio 提示词）

fixture：`tests/fixtures/official-reference/pravend-cluster/sources/pravend-official-cns-extract.txt`（179 字，4 句）
模型：`qwen2.5:latest` Q4_K_M，RTX 5060 Laptop 8 GB，Ollama loopback。
指令：Studio 真实提示词（`prompt.v12`，Pass A 9221 字符 / Pass B 11027 字符）。

| 观测项 | v16 `num_ctx=32768` | v17 `num_ctx=8192` |
|---|---|---|
| Pass A prompt / eval | 4839 / 2302 | 4842 / 2223 |
| Pass A 总耗时 | 337.0 s | **98.8 s** |
| Pass B prompt / eval | 8839 / 1600 | **8192（=上限，被截断）** / 4096（=num_predict 上限） |
| Pass B 总耗时 | ~275.6 s | 179.9 s（失败轮） |
| 生成速度 | 8.25 tok/s（A）/ 6.95 tok/s（B） | **57.0 tok/s（A）/ 56.0 tok/s（B）** |
| 两轮合计 | 612.6 s | — |
| 结果 | `passed=true`，1 候选，`needs_review` | `status=failed`，`Unterminated string ... semantic_packet.packet_hash` |

证据文件：
- `_tmp/quick-authoring-e2e-v16-evidence.json`
- `_tmp/quick-authoring-e2e-v17-ctx8192-evidence.json`

### 已证实的机制
1. Pass B 提示词（8839 token）超过参考 Worker 的 `num_ctx`（8192）→ prompt 被截到 8192 上限。
2. 截断后模型输出跑到 `num_predict` 上限（4096）仍不闭合 → JSON 解析失败 → 整个 attempt 失败。
3. `num_ctx` 直接决定速度：8192 档 57 tok/s vs 32768 档 8.25 tok/s（≈6.9×）。原因推断为 32k KV cache 挤爆 8 GB 显存后
   部分层回落到 CPU；**未分离验证，属推断**。
4. 截断发生在 prompt 前端还是后端**未分离验证**（推断为前端，因为模型输出了 semantic_packet 的回抄，像是丢了「不要回抄」的约束）。

### 谁决定 `num_ctx`
Studio **不控制** `num_ctx`。链路是 `Studio → {worker}/awake/analyze → 该 Worker 自己调 Ollama`。
参考实现硬编码 `num_ctx=8192, num_predict=1800`（`tools/customer-delivery/local-worker-package-acceptance.ps1:424`）。
→ 因此 Studio 唯一能自己掌握的是**提示词体积**；把体积压进预算，等于同时拿到「不截断」和「快 6.9 倍」。

### 附带发现（同一实验暴露）
- 参考 Worker 的 `num_predict=1800` 也偏小：本次 Pass A 真实输出 2223 token > 1800 → **即便修好 ctx，参考 Worker 仍会截断 Pass A 输出**。
- Pass B 提示词构成（11027 字符）：`ResultSchema` 4755（43%）+ `QuickAuthoringPassBSchemaOverride` 1375 + pass 契约 ~1466
  + `SemanticWorkflow` 1212 + `CompleteStageContract` 1181 + `CommonPolicy` 1038。
  其中 `ResultSchema`/`CompleteStageContract`/`SemanticWorkflow` 描述的是「完整语义图」，而 Pass B 的 override 已明确把这些字段
  全部作废（候选只保留 7 个字段，propositions/claims/target_spans 全为 `[]`）。**43% 的提示词是自相矛盾的无效内容。**
  文件自带注释已两次记录该矛盾造成的真实故障：模型照骨架写出 `text=null` 占位 claim；以及写出 11 条 claim/11 个 span
  把一轮拖到 17 分钟、7900 输出 token。

## 2. 修复批次

### 批次 A（已落地，本轮完成）
**哈希误抄不再作废整轮**。`quote_hash` 是模型自检值，不是服务端权威哈希；权威哈希由服务端按实际引用重算。
- 改动：`src/Awake.WorldbookStudio.Core/AuthoringDraftResponseNormalizer.cs`（`NormalizeEvidence`）
- 保留：`AuthoringDraftContracts.cs:1301` 的「模型自报 quote 与自报 hash 必须自洽」检查不变（Parsed 层仍会拒绝不一致）。
- 验收：`Draft.Tests` 107/107 PASS（红用例 `probe: a mistyped quote_hash must not kill an otherwise exact quote` 转绿）。

### 批次 B（已实施并验证，见第 2.1 节）
**Quick Authoring 的 pass_a / pass_b 改用独立精简契约，不再拼装共用语义图骨架。**
- 目标：Pass B instruction 11027 → ≤ 3500 字符；Pass B prompt 8839 → ≤ 6000 token（= 8192 − 1800 输出预算 − 余量）。
  实测：instruction 4831 字符（未达 3500，但已满足真实预算）、prompt 5954 token（达标）。
- 改动面：`AuthoringDraftPromptCatalog.cs`（新增 pass 专属契约常量 + `Build` 分支），`Revision` v12 → v13。
- 保留：`CommonPolicy` 全文（不可信输入、review_only、不得补造、字段白名单）。
- 移除（仅针对 Quick pass）：`ResultSchema` 中已被 override 作废的语义图骨架、`CompleteStageContract` 中 pass B 不适用的条目、
  `SemanticWorkflow` 中「建立 propositions/claims」的步骤（保留「反向检查正文不得新增命题」）。
- 附带：Pass B 的 `semantic_packet` wire 体积（Pass B prompt 中约 4400 token 属于请求体）需一并评估是否可裁。
- 验收（结果见第 2.1 / 2.2 节）：提示词进预算、两轮不触顶、覆盖率 6/6、建档 `needs_review` 全部达成；
  「`num_predict=1800` 下也成功」这一条**未达成**，且已证明原因在 Worker 侧输出预算，不在 Studio 侧（见 2.2）。
- 通道：改提示词版本 → 标准通道（一次独立审查 + 一次修订）。

## 2.1 批次 B 实施结果

改动：`AuthoringDraftPromptCatalog.cs` —— Quick Authoring 的 pass_a / pass_b 改用 `QuickPassAContract` / `QuickPassBContract`
（新增），删除共用的 `SemanticWorkflow` + `ResultSchema` + complete 阶段契约在该路径上的拼装，以及已被取代的
`QuickAuthoringPassASchemaOverride` / `QuickAuthoringPassBSchemaOverride`；`Revision` v12 → v13。

实测（同一 fixture、`num_ctx=8192`）：

| 观测项 | v17（v12 提示词） | v19（v13 紧凑提示词） |
|---|---|---|
| Studio 指令字符数（Pass A / Pass B） | 9221 / 11027 | 4601 / 4831 |
| Pass A prompt / eval | 4842 / 2223 | 3142 / 2178 |
| Pass B prompt / eval | 8192（触顶截断）/ 4096（触顶） | **5954 / 973** |
| 两轮总耗时 | 278.7 s（失败） | **126.5 s** |
| 结果 | `status=failed` | `passed`，1 候选，覆盖 6/6，`needs_review` |

与可用基线 v16（`num_ctx=32768`，612.6 s）相比：**612.6 s → 126.5 s（4.84×）**，且不再依赖大 ctx。
引用审计 4/4 `quote_locatable=true`、`quote_hash_ok=true`、`review_status=pending`。
证据：`_tmp/quick-authoring-e2e-v19-ctx8192-compact-evidence.json`。

### 红队发现的两次回归（已修）
1. **propositions 重复编号**（v18 失败）：紧凑契约漏掉了原骨架里的「id 不得重复」规则，模型输出了 9 条命题、id 重复 →
   `WB-AI-DRAFT-FORMAT-JSON: propositions 包含重复编号`。已补：id 唯一 + 「propositions 引用的每个 source_origin_id
   必须被某条 fact 的 evidence.reference_id 绑定」+ 「资料每一句都必须被 fact 证据覆盖」。
2. 正文逐字搬运仍会出现（v19 有 1 条 warning）：这是 v16 就存在的既有行为，不是本次回归；warning 会提示人工润色。

## 2.2 已证实的剩余缺口（Worker 侧输出预算）

用参考 Worker 的原参数（`num_ctx=8192, num_predict=1800`，与 `local-worker-package-acceptance.ps1:424` 一致）复测：
- Pass A prompt 3144（不再触顶），但 `eval_count = 1800`（正好等于 `num_predict`）→ 输出被截断 →
  `Unterminated string. Expected delimiter: ". Path 'propositions[6].object'` → `status=failed`。
- v19 实测 Pass A 真实需要 **2178** 输出 token（v16 需要 2302）。→ `num_predict=1800` 连 179 字的样本都不够，
  而参考 Worker 的 ctx 明明还有 5000 token 余量。

结论：**Studio 侧的上下文问题已解决；剩下的是 Worker 侧把输出预算写死成 1800。** 这正是批次 C/E 的必要性，且现在有硬数字。

### 批次 C（协议预算，待批准实施）
**Studio 显式声明生成预算，不再依赖 Worker 猜。**
- `BuildWorkerRequest` 增加 `generation: { required_context_tokens, max_output_tokens, prompt_characters }`；
  旧 Worker 忽略未知字段，向后兼容。
- 文档写明最低要求（建议 `num_ctx ≥ 8192`、`num_predict ≥ 2500`）。
- 修正 `tools/customer-delivery/docs/本地Worker配置.md` 的过时断言：该文说「界面不提供填写本机 Worker 地址的入口」，
  但 `wwwroot/index.html` 有 `localWorkerUrl` 字段、`Program.cs:454-465` 也确实保存（X1 能力）→ 文档与实现矛盾。
- 验收：契约 JSON 可解析；Draft/Workstation/BatchTests 全绿；文档与实际 UI 一致。

### 批次 D（证据完整性，归属待确认）
`tools/customer-delivery/local-worker-package-acceptance.ps1` **没有验收真实生成**：
- `:430` 调真实 Ollama，`:431-432` 只记录输出 sha256，`:433` 解析出的 `$result` **从未使用**；
- `:434-493` 用硬编码的 `$quotes/$titles/$summaries/$expressions/$ids` 拼装 3 个候选后返回；
- `:419` 的 system 提示词是 5 行英文桩，**未下发 Studio 的 `instructions`**。
→ 该「验收」只证明「协议握手 + 模型回了合法 JSON」，不证明内容、引用、覆盖率；其 8192 ctx 也从未被真实长提示词压过。
建议：改为消费 `$rawModelContent`，或在证据里显式标注 `model_output_discarded=true` 并降级命名为协议冒烟。

### 批次 E（参考 Worker 参数，归属待确认）
参考 Worker 的 `num_ctx=8192 / num_predict=1800` 为硬编码，且实测两项都不够用（prompt 8839、Pass A 输出 2223）。
建议：按批次 C 的 `generation` 预算计算，或至少提到 `num_ctx=16384 / num_predict=2500` 并写明依据。

## 3. 红队记录

### 第一轮（否掉自己的假说）
| 假说 | 结论 | 依据 |
|---|---|---|
| 提示词瘦身能省 25~30% 时间 | ❌ 否 | prompt 评估只占总耗时 2%（3.9 s / 337 s） |
| 91 s 残差是模型重载 | ❌ 否 | `load_duration` 实测 2325/92/72/69/94/91/79 ms，模型常驻 |
| 模型频繁抄错哈希是高频故障 | ❌ 否 | 探针 24/24 全对（6 次 × 4 哈希），16k/32k 复测各 4/4 |
| span 折叠能省时间 | ❌ 否 | 估算仅值 ~4 s |

### 第二轮（新方案的压力测试）
| 挑战 | 结论 |
|---|---|
| 「瘦身提示词会让模型变差」 | 反证充分：被删的是 override 已明文作废的骨架，且该矛盾已两次造成真实故障。仍需批次 B 验收实测。 |
| 「只瘦身指令就够了吗」 | ❌ **不够**。Pass B prompt 中约 4400 token 是请求体（semantic_packet/source_origins）；来源变长时预算会再次爆掉。→ 必须补批次 C 的运行时预算声明，否则是「静默截断」换了个地方复发。 |
| 「为什么不直接让 Worker 用大 ctx」 | 32k 实测 8.25 tok/s（一轮 10 分钟），16k 约 25 tok/s。策略应是「先压进 8192 拿速度，预算不够则显式要求更大 ctx（慢但正确）」，而不是静默截断。 |
| 「改 v12→v13 会牵连什么」 | 已核查：全仓仅 `_tmp` 日志与目录常量引用 `prompt.v12`，迁移候选/证据文件均未内嵌该版本号；`a4` golden 的 `request_hash` 是占位 `1111…`。→ 影响面可控。 |
| 「哈希门禁放宽会不会放过真幻觉」 | 不会：Parser 层仍强制「模型自报 quote 与自报 hash 自洽」，Quick 门禁仍强制 `quote` 逐字等于服务端 `source_origin.quote`，权威哈希由服务端重算。放弃的只是「模型抄错自己的哈希」这一无信息量的否决条件。 |

### 尚未证实（不允许当成结论写进交付）
- 截断发生在 prompt 前端还是后端：未分离验证。
- 32k 变慢的具体原因（KV cache 挤爆显存 / CPU 回落）：未分离验证。
- 批次 B 的瘦身后模型是否仍稳定遵守契约：未实测，必须先测再加断言。

## 4. 边界

本轮未启动 Bannerlord、未同步游戏目录、未访问真实云端 Provider 或 API Key；
仅访问本机 Ollama（`127.0.0.1:11434`）；未改真实源目录、未改 5 个迁移候选、未生成新的 rewrite candidate；
未把任何候选标为 approved / canon / compiled / published / runtime-ready。
