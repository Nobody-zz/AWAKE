# Worldbook Studio 批 2（生成正确性核心 11 条）执行结果（2026-09-10）

> 批次定义见 `WORLDBOOKSTUDIO-REDTEAM-REMEDIATION-PLAN-20260910.md` 第 3 节「批 2」。
> 前置：批 0 已定案、批 1 已交付（见对应两份记录）。
> 硬边界（全程遵守）：未启动游戏、未同步游戏目录、未访问云端 Provider / 真实 API Key / Worker / 网络服务。

## 1. 逐条结果

| 编号 | 问题 | 结论 | 主要改动 | 证据 |
|---|---|---|---|---|
| G2 | 只校验"子集"不校验"覆盖"，命题可被静默丢弃 | **已修** | Pass B 投影后按候选并集计算覆盖，未覆盖即阻断 `WB-AI-DRAFT-PASS-B-422`（含未覆盖数与示例）；`operation=drop` 的 span 及其 claim/所属 proposition 记为"有意排除"，不算静默丢弃 | 新增用例：必须覆盖冻结包 / drop 视为有意排除 |
| G3 | 候选分割没有服务端边界信号（"分割莫名其妙"的结构性根因） | **已修** | Pass A 顶层必须输出 `clusters`（id/title/proposition_ids/claim_ids/target_span_ids），Pass B 只能按边界分组；跨边界/拆边界/合边界/丢边界分别阻断；候选上限 12 超限阻断 | 新增用例：必须遵守 Pass A 边界 / 边界数量上限 |
| G4 | 风险启发式把"同引文不同侧面"误判为冲突红 | **已修** | 改为按命题语义判互斥（同 subject+predicate 下 affirmed↔negated；同 object 且 time_scope 互斥），不同主语/谓语/对象不再判冲突；缺命题结构时保留旧规则 | 新增用例：同引文不同侧面**不**判冲突 / 极性相反**仍**判冲突 / 时间范围互斥**仍**判冲突 |
| G6 | 本地链路不发系统提示词、无 prompt 版本协商 | **已修（最小口径）** | 本地请求声明 `prompt_revision`/`normalization_revision`；回执**不一致 → 阻断**（`WB-AI-DRAFT-PROMPT-409`）；回执**缺失 → 放行**（既有 Worker 不回执，硬校验会打断现网链路） | 新增用例：Worker 版本回执 |
| G7 | `source_text` + 逐句引文近似 2 倍膨胀 | **已修** | Pass A/B 且引文拼接等于整篇时，不再发 `source_text`，改发 `source_text_included=false` + `source_text_via="source_origins"`；其余链路（含本地 Worker、历史单段式请求）保持原样，`request_hash` 仍覆盖 `source_text` | 新增用例：请求体去重 |
| G8 | source origin 的合成 locator 与落库证据口径冲突 | **已修** | locator 统一为来源文件标识；旧合成 locator（`source unit NNNN`）仅判等容忍、不改写结果；提示词同步为"必须原样使用对应 source_origin 的 locator" | 新增用例：locator 跟随来源文件 |
| G9 | 两份死提示词常量与现行合同矛盾 | **已修** | `AuthoringDraftRequestSerializer` 的 `SystemPrompt`/`CompleteSystemPrompt` 删除（`rg` 确认无引用） | 编译 0 警告 |
| G10 | 摘要为空时写入"待补充：…"占位文本 | **已修（改为明确阻断）** | schema 把 `summary` 定为根 required 且 `minProperties:1`、值 `minLength:1` → "空"不可表达，因此选择**不写占位、明确阻断**（`WB-AI-DRAFT-422`，文案说明不会用占位文字代替作者内容） | 新增用例：空摘要（`"   "`/`null`）阻断；整份 JSON 不含"待补充" |
| G11 | 警告被硬截断在 64 条 | **已修** | 溢出产出结构化 `coverage.warnings`（`count = displayed + dropped`、`limit`），前端显示"警告共 M 条，本次显示 N 条（上限 64 条）"；未溢出时该键缺席 | 新增用例：警告溢出结构化信号 |
| N3 | 归一化阶段 `Limit()` 静默截断 | **已修（告警口径）** | 采集截断信号，输出 `coverage.truncations` 结构化条目（字段、原始/保留字符数），warnings 里给前 8 条与汇总；**选择告警而非阻断**（截断后仍在契约上限内可用，阻断等于废掉整份结果） | 新增用例：截断结构化信号 |
| B3 | 兼容分阶段流程预填 9 条"西帝国"示例视角（两处副本） | **已修** | 清空预填、删除重复的 `draftLegacyDefaultPerspectives()`，收敛为单一来源（空） | `editor-content.test.js` 的 `requestedPerspectives:[]` 断言仍绿 |

## 2. 冻结的字段契约（后续批次必须按此对接）

```text
coverage.packet_coverage        = { candidate_count, status, propositions|claims|target_spans:
                                    { total, covered, uncovered, uncovered_ids,
                                      deliberately_dropped, deliberately_dropped_ids } }
coverage.candidate_boundary     = "enforced" | "missing"
coverage.truncations            = { status:"truncated", count, items:[{field, original_characters, kept_characters}] }
                                  不截断时该键缺席
coverage.warnings               = { status:"truncated", count, displayed, dropped, limit:64 }
                                  未丢过警告时该键缺席（缺席即"没溢出"，不要显示兜底文案）
coverage.source_proposition_count / supported_proposition_count / unsupported_proposition_count
  · unsupported = 被静默丢掉的冻结命题数（不含 Pass A 有意 drop）
  · 三者恒在 Pass B 成功结果里，由**服务端权威计算**，不采信模型自报值
```

新增错误码：`WB-AI-DRAFT-PASS-A-422`、`WB-AI-DRAFT-PASS-B-422`、`WB-AI-DRAFT-PROMPT-409`
（前两个批 1 已补进 `SafeMessage` 与前端映射，`WB-AI-DRAFT-PROMPT-409` 按 `WB-AI-DRAFT-*` 前缀兜底）。

## 3. 用户可感知的行为变化（重要）

1. **"静默丢内容"变成"明确失败"**：候选并集没覆盖冻结命题 → 本次生成被阻断并给出未覆盖条数与示例。这是刻意的取舍：宁可失败，也不给"看起来成功、其实少了一半"的结果。
2. **切分不再由 Pass B 自由决定**：Pass A 先给候选边界，Pass B 只能照边界分组；**单个候选的边界被拆/被合/被丢都会阻断**。
3. **候选数量上限 12**：超过即阻断。
4. **摘要为空不再写占位文字**：建档时明确失败，要求先填/先生成摘要。
5. **兼容性口子（必须知道）**：若 Worker 不回执提示词版本，仍放行（只声明不阻断）；若 Pass A 不输出 `clusters`，降级放行并记 `coverage.candidate_boundary="missing"`。这两处是为了不打断既有本地 Worker，属刻意的兼容设计，不是漏改。
6. **本地推理成本下降**：Pass A/B 场景不再把整篇资料发两遍（等价于把输出预算问题减半）。

## 4. 测试与打包证据

```text
scripts\test.ps1        TEST_EXIT=0（全流程）
  · PASS: editor session harness (12/12)
  · PASS: editor safety harness (4/4)
  · PASS: draft DOM/state harness (5/5)
  · PASS: Worldbook Studio harness (113/113)
  · PASS: editor content core checks (11/11)
  · PASS: Worldbook Studio BatchTests (23/23)
  · DRAFT TESTS: 94/94 PASS          ← 批 1 为 83/83；批 2 新增 11 条
  · WORKSTATION TESTS: 12/12 PASS
  · PASS: Authoring save HTTP smoke / PUBLIC CONTRACT SMOKE: PASS
scripts\build.ps1       0 个警告 0 个错误
scripts\package.ps1     PACKAGE_EXIT=0，TEST: PASS，CONTRACT: PASS
```

## 5. 新发现（进 backlog）

| 编号 | 发现 | 去处 |
|---|---|---|
| X6 | `AuthoringTemplateFactory.cs:33,50,59` 手工新建档案的模板仍写"待补充：…"占位（与 G10 同类，但属模板而非 AI 建档；去掉需要 schema/契约口径） | 批 5/6（需先定 schema 口径） |
| X7 | G6 的"Worker 未回执版本"目前只放行、**无结构化信号**，前端无法提示 | 与 X1（Worker 配置入口）一起在批 4 处理 |

## 6. 未验证项与剩余风险

- **没有用真实模型跑过一次完整端到端**（本批只做离线契约与投影层验证）。因此"未覆盖即阻断"在真实资料上的**阻断率未观察**——提示词已引导"默认单 cluster、不得为凑数拆分"，但这条需要真机跑一轮才能确认不会过度失败。
- G4 的"同引文不同侧面"只有投影层单测 + 规则验证，未构造真实模型数据；**没有命题结构的候选仍走旧文本规则**，仍可能判红。
- 前端新增文案与"未覆盖/被截断/警告溢出"提示只做 DOM 桩验证，未在窄面板实测换行与读屏播报。
- 批 1 的同类提示：本批改动同样**尚未重新打成客户交付包**。

## 7. 边界声明

未启动 Bannerlord；未同步游戏目录；未访问真实云端 Provider / API Key / Worker / 网络服务；未修改真实源目录与五个世界书迁移候选；未生成新的世界书 rewrite candidate；未把任何候选标记为 approved / canon / compiled / published / runtime-ready；未修改 AWAKE 模组本体、ModuleData、dist 或冻结构建产物。临时探针脚本全部删除。
