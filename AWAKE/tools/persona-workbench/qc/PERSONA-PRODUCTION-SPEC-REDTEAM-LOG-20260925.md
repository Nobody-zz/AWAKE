# 制作规范候选稿红测记录（2026-09-25）

## 范围与结论

检查对象是 `PERSONA-CARD-PRODUCTION-SPEC.zh-CN.md`、`persona_aifeel.py`、真实 Persona DSL 模拟器和提示词渲染器。没有修改角色卡、tag registry 或游戏目录。

本记录是**作者自测**，不是独立规范验收，也不是角色卡质量认证。当前结论：候选规范的部分反例已有可复现支撑；尚不能切换为权威规范。

## 可复现结果

### 1. 测试与构建

- 命令：`dotnet build AWAKE/tools/worldbook-runtime-sim/WorldbookRuntimeSim.csproj -c Release`
- 结果：exit 0；0 errors，1 个既有目标框架兼容警告 `NU1702`。
- 本轮刷新 Release 链路资产：`dotnet build AWAKE/tools/worldbook-runtime-sim/WorldbookRuntimeSim.csproj -c Release` exit 0（0 错误，1 个 `NU1702` 兼容警告）；`dotnet build AWAKE/tools/worldbook-runtime-production-smoke/WorldbookRuntimeProductionSmoke.csproj -c Release` exit 0（0 警告/0 错误）。之后用新产物重跑整套回归，32/32 通过。
- 单测：`py -3 -m unittest discover -s AWAKE/tools/persona-workbench/qc -p test_persona_aifeel.py -v`
- 当前结果：34 tests，全部通过。测试包含模板槽位/连接词变异、相同句式但决策不同的负对照、简单证据有限 DSL、地点关联不能推导职权、DSL `DATA_CN` 严格解析、实际生成器裁剪、required-claim 保留/逐卡完整覆盖/空清单 fail-closed、Ollama 元数据精确匹配、HTTP 错误响应正文保留、未知事实人工复核队列、最简源卡真实物化→DSL→生产模板的端到端载荷保留，以及简单范围/超范围规范反例和审查包哈希一致性。

### 2. 首轮五类规范反例的历史证据

| 夹具 | 已执行的证据 | 当前结果 / 限制 |
|---|---|---|
| A 换槽模板 | `test_new_slot_filled_prose_is_detected_without_old_corpus` 与 `test_synonym_swaps_do_not_erase_the_sequence_family`：构造仅改变名词/动词/连接词的同骨架语句。 | 形态读数能定位指定“先—再”族；这不是语义裁决。审稿人仍需证明不同情境中的选择和理由相同。 |
| B 地点越权 | `test_place_link_does_not_authorize_boundary_action`：唯一 link 为 `confirmedFor=place`，卡内却出现征税行为。 | 报告将其列为待核边界行为，不把地点关联提升为职权证明。 |
| C definition 有、prompt 无 | `test_real_generator_reports_trim_and_drops_optional_required_claim`：临时合成 approved definition，将超长核心与 `REQUIRED_CLAIM_SENTINEL_7F42` 放进可选公开段，调用 Release 模拟器、预算 6144 bytes。 | 真实生成器输出 `fallback=False`、`trimmed=True`，DSL 中 sentinel 不存在；确认必须检查最终 DSL/prompt，不能只看 definition。临时夹具在系统临时目录生成并自动清理。 |
| D 简单但有据 | `test_real_generator_and_template_accept_a_sparse_evidence_limited_definition`：只含身份事实与一条克制倾向，走 Release 真实生成器和生产模板；另由 parser 单测检查稀疏 DSL。 | `fallback=False`、`trimmed=False`，两条有据文本都保留在最终 prompt；不要求补足边界字段。合成夹具只证明技术链路不会因字段少而拒绝，不替代人物语义人工判断。 |
| E 相似句式负对照 | `test_different_meanings_with_same_sentence_frame_remain_a_locator_not_a_verdict`：同一“先—再”句式分别表达拒绝出兵与有限支援。 | 指标仍报告句型线索，但没有自动 verdict；规范规定句型相似本身不能判失败。 |

### 3. 真实角色卡离线链路

先前完成的三张斯特吉亚卡离线链路快照：4 个情境 × 2 次采样 × 3 张卡，共 24 份实际渲染 prompt；0 fallback、0 trimmed。该历史轮**未调用模型**，因此没有模型回答或盲评结果。

报告：系统临时目录 `awake-persona-spec-v2-dsl-metrics.json`，SHA-256 `06dec52eecbc7caa07591b835d202d0d10119f872c7f35f51ab4a8c3db88fad1`。报告将 `definition_field_diagnostics` 与只解析 DSL `DATA_CN` 的 `runtime_dsl_metrics` 分开；本轮 `summary` 出现在三个 definition，但没有出现在 DSL 统计中。

### 4. Ollama 本地模型与事实边界红测

- Ollama server：`0.23.1`。
- `ministral-3:8b` 不适用于本机这一轮：短诊断请求返回 HTTP 500，响应为需要 22.0 GiB system memory、当前可用 18.1 GiB；未将失败请求伪记为样本。
- 使用已安装且短请求成功的 `qwen2.5:latest` 重跑真实链路：三卡 × 四情境 × 两个 seed（1700、1701），共 24 次串行本地 `/api/chat`。模型 digest：`845dbda0ea48ed749caafd9e6037047aa19acfcfd82e704d7ca97d631a0b697e`；温度 0.7、top_p 0.9、num_predict 500；平均接口总耗时约 3.63 秒/回答。
- 结果：24/24 返回基础结构有效 JSON；没有回答触发 chat `command` 禁止项。这只证明输出可解析，不等于质量通过。
- 未知事实情境的 6/6 回答都进入 `unknown_fact_review` 人工队列。作者抽查后，至少 3/6 擅自补出输入中没有的具体消息：乌里克提及奥莫尔城外截货和叛军勾结；亚恰娜称北境强盗所为；佐里卡称西比尔市猎人谈论北路风险。另有回答声称“确有所闻/最近有耳闻”，但 prompt 没有提供 NPC 已收到传闻的证据。不能据此直接判三张卡内容有错；这是本轮发现的共用 prompt/运行时事实边界风险，不要求卡作者补写传闻。
- 完整报告位于系统临时目录 `awake-persona-spec-qwen25-final.json`，SHA-256 `f3eacc5974d6fca556cddf01c4bfd3c39172eb817bfcba3c544f917aaab1c059`。报告含逐条 prompt、卡/DSL/模板哈希、模型 digest、seed、参数和响应时延；未将整份角色内容复制到仓库。
- 本轮未传 `--claims` manifest，因此 `claim_retention.status=not_requested`；不能声称真实卡 required claim 已通过逐条保留检查。

### 4.1 候选规范整包的本地执行诊断（非独立验收）

- 输入：完整候选规范与无答案 A–E 夹具包；未提供答案钥匙。候选规范 SHA-256 `7368ce2436d65c24a650a894c1ad1144dbf8e3dde2baf2aac0e18088acc20eb6`；夹具包 SHA-256 `f10697e769c018b4dc1d1505c704af0ed9be1495b8ff3674383c4feac800a3ec`。
- 环境：Ollama `0.23.1`，`qwen2.5:latest`，digest `845dbda0ea48ed749caafd9e6037047aa19acfcfd82e704d7ca97d631a0b697e`；temperature `0.2`、top_p `0.9`、num_predict `1000`、seed `20260925`；串行单请求，耗时 `83.79s`。
- 原始响应与元数据报告：系统临时目录 `awake-persona-spec-qwen-review-20260925.json`，SHA-256 `6f2ca62c4b73bb8964e555dd8d0706cbbef633bad148e92f1e21a20e9c8b7810`；JSON 已重新解析验证。
- 结果：模型将 A/B/C/D/E 分别判为 `REVISE / REVISE / REVISE / PASS / PASS`。对照作者答案钥匙，D 的人格结论应为 `INSUFFICIENT_EVIDENCE`（可保留身份卡，但不能认证为已充分刻画的人格），所以模型产生了**错误放行**；C 虽然没有放行，却未明确指出 `runtime_required` Claim 已从 DSL 和最终 prompt 丢失，未给出技术阻断处置。A、B、C 多数条款引用也没有准确落到决定性验收规则。
- 解释边界：这是单一模型模拟执行，不是独立人工评审，也不足以区分规范歧义与模型漏读。它证明不能让本地模型充当规范验收者或自动裁决器；后续应让盲审者给出最终状态、`approved` 是否允许，以及精确条款/证据，而答案钥匙仍须在人类锁定后核对。该轮不构成 A–E 通过证据。
- 流程修正：核对审查材料后发现，夹具包原先引用的是面向真实角色回答的通用盲评模板，缺少逐夹具的“规范第八节状态 / approved / 技术门”记录栏。新增专用无答案审查表 `qc/PERSONA-PRODUCTION-SPEC-REVIEW-FORM-20260925.md`，并同步修改夹具包与候选规范第十节；原模型回复仍只作诊断，不重算为独立审查。
- 规范修正：这次模型诊断误将 D 判为 `PASS`，而规范要求证据只支持单一情境判断时保留身份草卡并标为 `INSUFFICIENT_EVIDENCE`；对 C 也未清楚映射“`runtime_required` Claim 不在最终 prompt”到技术阻断。已在候选第八节新增明确判定顺序：已知运行时硬门缺陷先 `REVISE`，链路证据不完整为 `INDETERMINATE`，资料不足以支持两项不同情境判断为 `INSUFFICIENT_EVIDENCE`，仅在充分证据、硬门和人工盲评都通过后 `PASS`。此次本地模型结果对应修订前的规范 SHA-256 `7368ce2436d65c24a650a894c1ad1144dbf8e3dde2baf2aac0e18088acc20eb6`；修订后的版本必须重跑夹具，不能沿用旧结论。
- 首次修订回归：修订版 SHA-256 `e0b78f5a5bd0cd3c8692482819af30b152eea6dbf7e1d63e79ef04c86e941227`；同一 Ollama 模型/digest 与固定参数下，A–E 结果仍为 `REVISE / REVISE / REVISE / PASS / PASS`。C 开始引用第六节链路硬门，但动作仍写成“确认是否体现”，未明确按给定证据直接阻断；D 再次错误 `PASS`，把“少于最低人格证据”误读为可通过，并引用了否定该结论的条款。该输出表明当前判定规则对模型读者仍不够显式，且模型自身漏读；不能仅凭此区分两者。
- 针对上述剩余误读，第二次修订在第五节明确禁止为了差异化而制造无据不同，在第八节将“有据的不同情境判断数量”与状态显式映射为 `0–1 → INSUFFICIENT_EVIDENCE`、`≥2 但流程未完 → REVISE`、其余硬门通过后才可继续评估 `PASS`。第二次修订后尚未复测，不能声称 D 已不再误判。
- 第二次修订回归：候选 SHA-256 `e10bb257bc92f2db4220bb43799989402c45a77aced17502e79b43759464c51e`，夹具包 SHA-256 `58c81f7a0b60a9f457f9d3a5cb8a51f511209d672f2124054788c9579fa08c70`；Ollama `0.23.1` / `qwen2.5:latest` 同 digest，参数及 seed 同前，耗时 `97.26s`。机器可读报告在系统临时目录 `awake-persona-spec-qwen-review-20260925-r3.json`，SHA-256 `a801c428aba1f6f37ccac77f626e9513fe8930d686b238e4ff7fd24a4058fa42`。
- 回归结果仍为 A/B/C/D/E = `REVISE / REVISE / REVISE / PASS / PASS`。D 继续无视明确的 `0–1 → INSUFFICIENT_EVIDENCE` 映射而误放行；B 把卡片代号写错；C 只泛称应重新检查，未按夹具已提供的“最终 prompt 不含 C1”直接说明硬门阻断。A/E 结论方向正确，但多处条款引用不精确。判定：Qwen 不适合作为该规范的可靠执行者或验收者；该轮不证明规范通过，也不单独证明条文本身仍有歧义。后续验收必须由两名独立人类按专用表逐例引用规范中的状态映射与证据。
- 用户确认 Ollama 已运行后，检查 `/api/tags` 确认 `ministral-3:8b` digest 为 `1922accd5827ebe6829e536369195db25eaf664528dc66206d646ea3bb386b71`。完整规范+A–E 一次性输入在 240 秒超时；没有重试整包。
- 为隔离 D 的单一未决口径，另做一次定点诊断：只输入当前候选第 3/8 节和 D 夹具，`temperature=0`、seed `20260925`、`num_predict=600`，prompt/completion 1495/527 tokens，耗时 59.4 秒。模型按现行明文判为 `INSUFFICIENT_EVIDENCE`，并指出门槛要求两种情境；但它提出“同一行为在不同变量下计作两个判断”的放宽建议可能制造凑场景通道，不能直接采纳。该输出支持状态映射可被读取，不解决“有限范围简单卡是否可获 PASS/approved”的产品选择。输入和完整响应见 `qc/PERSONA-PRODUCTION-SPEC-MINISTRAL-DIAGNOSTIC-20260925.md`。

### 4.2 有限范围可用性修订（候选 v2.1）

- 完整 A–E 输入曾对 `ministral-3:8b` 请求 240 秒后超时，没有可用结果，也没有重试整包。
- 根据用户目标“简单但有据的角色不被误伤”，候选规范改为：先写明卡片范围；PASS/approved 只适用于该范围，不声称完整人格认证。草图不设条数门槛；资料不足时缩窄范围，而不是补造第二个行为。与声明范围重叠的跨卡重复仍可判问题，未覆盖情境只检查是否诚实承认未知。
- 新增夹具 F：证明 D 的有限范围许可不能放行同卡里额外添加、无证据的野心/忠诚主张。D 明确拆开“有资格在窄范围 PASS”和“本夹具尚缺真实运行链/盲评，因此不能直接做生产 PASS”。
- 交叉核对规范引用的下游盲评模板时发现其旧版固定列出 A/B/C、强制跨卡比较；这会抵消有限范围修正。已改为声明范围随匿名样本提供、只在范围实质重叠时比较、无重叠可标 `NOT_APPLICABLE_WITHIN_DECLARED_SCOPE`；身份卡不得被要求扩写。
- 因此审查包改为哈希锁定五份必要输入：候选规范、专用审查表、A–F 夹具、作者记录模板、生产盲评模板。专用表分开询问“范围内通过资格”和“本夹具当前证据是否足以直接批准”，避免再次混淆这两个状态。
- 该设计是本轮按用户总目标采用的工作口径，尚未经独立人类盲审，也不代表用户对正式替代旧规范的最终签收。当前候选仍为未生效版本。
- v2.1 的 D/F 定点复测：`ministral-3:8b`（同 digest），输入第 3/8 节及 D/F、seed `20260925`、`num_predict=700`，耗时 `77.9s`。模型判 D 无缺陷且只能在声明范围内通过、F 的无据主张应删/补证；但 D 的实际生产结论写得含混，且把规范条款错引为“3.1 / 4.1”。方向信号通过，精确引用质量失败；不能据此替代人类验收。详见 `qc/PERSONA-PRODUCTION-SPEC-MINISTRAL-DIAGNOSTIC-20260925.md`。

## 本轮实际修正

1. 检查器增加真实 DSL `DATA_CN` 解析，definition 字段诊断与运行时 DSL 统计分离；遇到 section 外 DATA_CN、非 JSON 字符串时 fail closed。
2. 增加同句式异含义负对照，规范明确不得单凭句式、文化或职业相似判红。
3. 加入真实生成器裁剪测试和简单证据有限 DSL 测试。
4. 新增作者记录模板与盲评模板，并在候选规范和检查说明中链接。
5. Persona Workbench README 只增加候选稿提示，明确旧规范和旧门禁仍是当前权威，未提前切换。
6. 可执行性复核后，将单卡日常质量复核改为“一位独立盲评者可给 PASS；证据歧义、作者补写、匿名失败或有据申诉才升级第二人”。候选规范自身的替代验收仍保留两位独立评审者，不与单卡流程混淆。
7. 将规范盲审材料拆成无答案的 `PERSONA-PRODUCTION-SPEC-REVIEW-FIXTURES-20260925.md` 和作者保管的 `...-ANSWER-KEY-20260925.md`；更新 §10 让评审者在锁定结论后才对照答案。盲评模板第二阶段增加逐样本实际 prompt 知识/记忆/玩家输入记录，并要求先随机别名和独立检查去标识泄漏。
8. Claim manifest 增加逐卡覆盖 fail-closed：每个选中卡必须显式出现，确无必需主张时写空数组；漏卡不得被解释成“无 claim”。
9. 读取实际 `run-card-gates.ps1`、`audit-character-enhancement.ps1`、`audit-character-text.ps1` 后，在候选 §8 写入获批后的逐门禁迁移表。特别标明当前 enhancement E1/E2/E2b/E4 会阻断并要求填写草稿/固定字段；候选获批前不改当前 README 或 gate runner。
10. 新增真实最简卡链路测试后，首次运行被阻断：`materialize-definitions.ps1` 的 `Get-NonEmptyStrings` 在空输入时沿 PowerShell pipeline 返回 `$null`，把契约要求的空数组写成 JSON `null`；生成器因此将角色评估标为 `EVALUATION_BLOCKED`。同一 helper 还在脚本中重复定义。已删除完全重复定义，并以 `return ,$out` 保留集合对象；回归夹具同时包含空边界数组与单条有据 `realSelfBehaviors`，现在完整链路 `fallback=False`、`trimmed=False`，两条 required claims 均进入 DSL 和最终渲染 prompt。测试仅在系统临时目录创建合成卡/sidecar/claim manifest/definitions，不触碰仓库角色卡。

## 未完成 / 不可声称

- 未完成两名独立评审者盲评六类规范夹具；本记录不得冒充独立通过。
- A–F 目前是评审包和答案钥匙的作者侧制作，还没有独立评审结果；答案钥匙不得随候选规范一同发给评审者。
- 本次 Qwen 只有作者单人抽查，没有两位独立盲评；不能将回答数值描述成经过校准的 AI 味分数或独立通过结论。
- 只读盘点发现角色目录 355 张卡全部为 `draft`；不能挑一张尚未签收的在制卡冒充“已验证的简单角色”。因此 D 使用了证据范围明确的合成夹具，真实卡的简单正例仍待内容端提供或签收后再选。
- 六个规范夹具目前在规范文本中作为固定评审输入；C/D/E 有既有机器测试支撑，A/B/F 的语义最终判断仍需盲评者按模板完成。
- 尚未切换 README 旧流程与 `run-card-gates.ps1`；未认定候选稿可替代旧规范。
- CLI 已支持 `--claims` 对必需主张做 DSL 与最终渲染 prompt 精确保留检查，并在未提供时明确输出 `not_requested`；仍未支持安全匿名盲评包，正式盲评需按模板由独立保管人制作。
- 本地模型的 `unknown_fact_review` 只保证把回答放入人工队列，不自动判定其中事实真伪；审核人必须逐条对照该样本的完整 prompt 和来源证据。
- 当前工作口径已消除“单一情境自动阻止整卡 approved”的旧门槛；候选 v2.1 允许范围受限的 PASS，但要求范围记在作者记录并绑定卡片 SHA-256。Ministral 已初步读出 D/F 方向，但未准确给出 D 的 fixture-level 状态；仍需人类审查确认状态分层和追溯要求能被稳定执行。
- 简单角色证据目前为合成端到端正例：355 张在制卡均为 `draft` 且已有丰富运行时字段，不能挑一张冒充“简单、已签收”的真实正例。合成卡已验证只含身份事实和一条行为、其余边界数组为空时仍可穿过真实链路；这证明不因空字段而技术阻断，不证明真实角色内容质量。
- v2.1 与新增 F、下游模板修订后已重算五份审查包输入哈希；最后一次单测 34/34 通过，其中包含 exact-hash 和 scope consistency 测试。当前可用包尚未有人类签署，两位独立人类评审尚未开始。

## 复跑命令

```powershell
dotnet build AWAKE/tools/worldbook-runtime-sim/WorldbookRuntimeSim.csproj -c Release
dotnet build AWAKE/tools/worldbook-runtime-production-smoke/WorldbookRuntimeProductionSmoke.csproj -c Release
py -3 -m unittest discover -s AWAKE/tools/persona-workbench/qc -p test_persona_aifeel.py -v
```

真实卡链路的选卡、临时输出及 Ollama 命令见 `qc/PERSONA-AIFEEL-README.md`；将来做盲评时必须使用 `qc/PERSONA-BLIND-REVIEW-TEMPLATE.zh-CN.md`，不可把原始含身份 chain 报告直接发给评审者。

## 2026-09-26：候选迁移表与现行观测项复核

- 对照作者规范 v4.2 与 `audit-character-enhancement.ps1` 的实际实现，发现文档新增 O6/M9（“他人起手 → 我回应”）及阈值说明，但脚本只实现 O2–O5，尚无 O6 计算或报告。候选 v2.1 的迁移表已明确这一实现缺口：不宣称 O6 已保留；若未来实现，先独立校准且仅作告警，不直接作为硬门。
- 发现审查包正文称每位评审者收到五份文件，下一句却误写“三份”；已更正并增加自动回归断言。
- 更新候选规范 SHA-256 与五件审查包清单；增加 `only_not` 同构正例与异构对照，验证它只作为定位读数、不自动生成质量 verdict。红测又发现“他不只查账，也核验印章”及“只要不下雨……”被误归为 `only_not`；现将条件结构优先识别，并排除“不只/不仅 + 也/还/更”，新增反例测试。`test_persona_aifeel.py` 当前 36/36 通过，`git diff --check` 通过。未修改角色卡，正式门禁和 README 均未切换。
- 本次没有调用 Ollama：当前 Codex 会话不存在批准的本地 Worker V1/V2 工具；按本地 Worker 技能，不绕过受控入口直接请求 Ollama。故本轮是代码/文档确定性复核，不构成新的模型诊断或独立人类审查。
