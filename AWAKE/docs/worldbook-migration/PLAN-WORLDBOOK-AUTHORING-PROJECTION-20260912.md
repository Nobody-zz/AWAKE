# PLAN: Worldbook Authoring v1 投影批 — 20260912

> 状态：`approved_signed_off`（E0 计划，2026-09-12 用户签收；独立审查 7 条修订已全部落实）。W1 已产出锚点绑定计划与 lore 实体清单。
> 前置基线：R3 修订批已签收（`docs/review-state/WORLDBOOK-SEMANTIC-R3-REVISION-BATCH-20260912.review.json`，
> 12 档 / 41 claim，独立审查 round 2 APPROVED + user_signoff）。
> 本批目标：把 12 个候选文档投影为 **Studio authoring 工作区内的正式档案**，并清偿全部审查挂账；
> 不编译发布、不同步游戏目录、不改任何 C# 代码（无 BuildId 需求）。

## 0. 输入与事实基础

- 候选：`D:\AWAKE-Archive\...\r3-revision\documents\*.r3.json`（12 档，place_cluster/split_from/layer 齐备）
- 决策记录：`SEMANTIC-R3-DECISION-RECORD-20260911.json`（deviation_log 五条 + claim_authority：doc 为权威版）
- Studio 工作区：`tools\worldbook-studio\workspace\`（authoring / authoring-v1 sidecar / compiled / export 结构齐备）
- 权威门：Authority Gate P0 已落地（register→approve→compile-proof 不可变凭证；未接线必须阻断发布）
- 实体登记表：`docs\mappings\persona-entity\`（current-pointer + 世代哈希，CAS 保护）
- 身份口径：手册 A5/A6（平民/头人/中间人/士兵/贵族 + 知识范围 本地/本国/跨国/秘密 + 详细度 摘要/常规/详细/机密）

## 1. 工作项（按依赖序）

### W1 实体盘点与两分建档（2026-09-12 用户定则修订）
- **实体两分**：游戏锚定实体（有代码对应，正式 ID）vs 语义实体（游戏无对应代码但知识世界自有一致身份，如卡恰尔半岛/拉科尼斯湖/黎明山脉/合儿必特部/阿赫哈克）。
  语义实体**不能因为没有实际参考就不建立联系**——走独立 lore 命名空间（`entity.lore.*`，绝不冒用正式实体 ID），
  在 persona-entity 登记表扩 lore 分区收录（数据变更，需过登记表 schema；编译器对登记表内 ID 即正常解析折入 keywords，
  对未登记 ID 宽容返回空——两种情况都不阻断编译，已核 `RuntimePackageCompiler.LookupEntityAnchorNames`）。
- 语义实体同一性：名称归一 + 所指一致 → 同一 lore ID，**人工映射、禁止自动猜测**；
  游戏侧交叉引用（卡恰尔半岛 ↔ town_S1 官方描述提及）作为元数据参照，不改变 lore 身份。
- 逐 claim 盘点产出 `ANCHOR-BINDING-PLAN.json`：每档 → 实体（game-anchored 或 lore）+ 依据；
  建档前未定者沿用 `authoring_provisional` 占位。

### W2 正式建档（12 档）
- 经 Studio 保存链写入 `workspace\authoring`（非直接写盘，走 API/保存链以获得审计事件与稳定内部编号）；
- 每档：标题=候选 title、domain/subdomain 按 taxonomy v1、客观事实=claims（fact/relation/state）、
  interpretation/rumor 段按各自类型建档、NPC 表达=expression spans（layer + 身份）、
  元数据带 place_cluster / split_from / decision_record 引用；
- 全部档案 `needs_review`，**不自动 approve**。

### W3 子引文细化
- 现状：origin 绑整 variant 引文；本批按 B6"引文粒度细于断言"逐 claim 定位子引文，
  生成新 locator + quote_hash（对应 v1 variant 内字符区间），替换整段引用；
- 同步修：a48e49e8 predicate 正式落位、kach-tales 框架句（首句并入首 span 或单列 framing claim 需裁定）、
  claim 元数据现代措辞统一（"考古与移民史佐证"→世界观内表述）。

### W4 grants/profile/scope 映射
- 每个表达段按 A6 口径落：身份（profile_id 取自 persona-entity 登记表；不在表中的身份——如"北行学士/跑船人"——
  映射到最近通用身份或触发**登记表扩充裁定**）、layer（rumor/summary/detail/secret）、scope、min_detail；
- 落守则红线：宁可窄授权，不默认"所有人"。

### W5 账目清理（迁移文档层，不动 Studio）
- worksheet 孤儿 `origin.r3.*`（14 个孤儿 claim）清理；der-04 action_map 归属修正（57e81a17 归 der-01/03 拆分）；
- SUPERSEDED-MAP 与 action-queue 对账基线统一为 22。

### W6 结构验证与证据
- Studio 本地校验（`/api/validate` 或 CLI）0 blocking；
- 三向闭合移植校验：档案内 assertion/expression 与 R3 claim 一一对应（复用 validate 思路对 authoring 产物再验）；
- 证据目标 E2；产物清单 + 校验报告落 `docs/evidence/`（该目录 gitignore，报告摘要入本计划追记）。

## 2. 明确不做

- 不 approve 任何档案（审批是用户在 Studio 内的人工动作，属 Authority Gate 主权）；
- 不编译进 `compiled\`、不 export、不发布、不替换 010 试点包、不同步游戏目录；
- 不改 C#/Studio 工具代码；不动 v1 源文、r2 候选、归档区；
- 对象簇嵌套裁定：倾向"区域档与地点档靠锚点平级关联，不做嵌套容器"——本批按此执行，异议在审查轮提出。

## 3. 风险与开放问题

| # | 风险 | 缓解 |
|---|---|---|
| 1 | 四个地理对象无游戏锚定实体 | W1 三分处理：game-anchored 挂靠 / **lore 命名空间建档**（登记表扩 lore 分区，编译器已核实容错）/ 暂缓占位 |
| 2 | 登记表可能不含"学士/跑船人/旅人"等表达身份 | W4 触发登记表扩充裁定，宁缺勿造 |
| 3 | 子引文细化工作量最大（41 claim × variant 定位） | 按 5 源条目分五小组提交，组间可独立校验 |
| 4 | Authority Gate 保存链对批量写入的会话/审计要求 | 建档走官方保存链；如遇批量限制，分批提交 |

## 4. 流程

本计划 → grill-me 拷问 → 独立只读审查 → 你签收 → 实施（W1→W6）→ 证据汇报。

## 5. 拷问修订（2026-09-12，GRILLME-PROJECTION-PLAN-20260912.md 全 12 问）

1. **unresolved claim 投影裁定**：B5 无 unresolved kind → 知识限制类 claim（96448244、fd78）不落断言，落档案"确定程度=存疑"+摘要句；纯注册元数据（ddfb30ad）不投影，留迁移文档层。
2. **表达身份映射预裁定**：不扩登记表——表达身份一律映射手册九类通用身份（学士→贵族学识口径、跑船人/渔民/山民→普通平民、行商→公证商人），来源身份保留在表达文本内。
3. **建档走 `POST /api/authoring/save-authoring`（CAS: sourceHash+revision）与 create-document 端点**，逐档 读→建→存；建档前对 editor-catalog 查重；正式编号由 Studio 生成，候选临时 ID 不带入。
4. **subdomain 稳定 ID** 从 `knowledge-taxonomy.v1.json` 取，禁止手填。
5. **place_cluster/split_from 归属**：authoring schema 白名单能放则放，放不下记入批次 `PROJECTION-MANIFEST.json`（候选 ID → 新档案编号 + 簇 + 血缘），不硬塞。
6. **r3-revision 冻结**：已批准基线不回写；W3 引文细化产物落 authoring 档案 + `QUOTE-REFINEMENT-20260912` 记录。
7. **实施顺序**：逐档串行 W2→W3→W4，W5/W6 收尾；W1 增"读登记表 schema 定 lore 分区扩展方式"；W6 增"闭合复验工具（assertion↔claim、expression↔span、layer/grants 一致性）"。
8. **审批与回滚**：用户 Studio UI 人工审批；建档失败逐档重试，已建不删、manifest 记状态。

## 6. 独立审查修订（2026-09-12，VERDICT REVISE → 7 条全落，修后可签收）

1. **【P0】"存疑"承载重裁定**：authoring.v1 schema 无认知确定性字段（唯一 certainty 是时间语义）且 sources XOR author_created 封死旁路。**裁定取方案 b**：unresolved 知识限制类 claim（96448244、fd78df11）投影为 **assertion kind=interpretation**（对知识状态的推断性陈述，正文口径"仅凭旧书说不清"本已合规），PROJECTION-MANIFEST 逐条记录 unresolved→interpretation 溯源标记，档案 summary 提及。ddfb30ad 仍不投影。
2. **【P1】lore 分区降级为独立清单**：不动 entity-registry schema/生成器/pointer（那是 persona 线的链条）。新增独立 `LORE-ENTITY-REGISTER-20260912.json`（ID/名称/别名/游戏交叉引用/来源依据），锚点仍写 `entity.lore.*`——编译器容错不折 keywords（已核），可检索性由 title/aliases 承担；**登记表 lore 合并显式推迟**到与 persona 线协调的批次，记 deviation。
3. **【P1】恢复 place_cluster→referralIds 接线**（撤销静默放弃）：每个表达段落 `fallback_referral_ids`，指向同簇兄弟档；referral ID 命名 `referral.cluster.<place>.<seq>`，登记进 referral-registry（数据文件）。
4. **【P2】W4 来源更正**：profile_id 取自 `docs/worldbook-studio-plan/profile-registry.v1.json`（12 profile），非 persona-entity 登记表；W4 首步产出九类身份→profile ID 字面映射表（学士建议→notable 而非 noble，回应"宁窄勿宽"）。
5. **【P2】split_from 伴随义务**：血缘指向 authoring_provisional 允许；必须同时落 `event_id = event.migration.r3-projection.20260912` + `revision = 3`；"临时 ID 不带入"仅指新档案自身编号由 Studio 生成，血缘引用保留 provisional ID。
6. **【P2】W5 精确路径**：仓库内 `docs/worldbook-migration/SEMANTIC-MIGRATION-ACTION-QUEUE-R2-20260906.json`（der-04 错位）；归档区 `SEMANTIC-WORKSHEET-R3-REVISION-DRAFT-20260908.json`（孤儿 origin）。"明确不做"改为：不动 `r3-revision\documents`（已批准基线）与 v1 快照，账目清理仅触及列名两文件。
7. **【P2】W6 闭合规格定数**：epistemic 映射表 source_fact→fact、relationship→relation、state→state、interpretation→interpretation、rumor→rumor、unresolved→interpretation（manifest 标记）；**预期 assertion 计数 40**（41 − 1 元数据 dd fb30ad）；**预期 expression 计数 16**（layer 标注 span 数）；排除清单 = {ddfb30ad}；kach-tales 框架句并入首 span。
8. **【P3 附带】** create-document 模板自带 author_created（author.developer）在填入 sources 时必须移除（oneOf 约束）；scope 落位用 schema 枚举 local/regional/national/faction/elite/private，手册"跨国"→national 映射记入 W4 表。

## 7. W5/W6 实施追记与口径订正（2026-09-12）

> 追加记录，不改写上方第 1～6 节原写内容。凡上方数值与本节冲突，**以本节实测为准**。

### 7.1 W5 实施结果

- **action-queue JSON 修复**：`SEMANTIC-MIGRATION-ACTION-QUEUE-R2-20260906.json` 结尾缺一个 `}`，解析在 line 302 失败。根因：未提交的 `corrections_20260912` 追加修正节收尾时吃掉了原收口括号；**仓库已提交（HEAD）版本为合法 JSON**，缺陷由本次未提交改动引入。已修复，解析通过。
- **der-04 action_map 归属修正**：已在 `corrections_20260912.der_04_attribution_fix` 落账（新 trade claim `57e81a17…` 归 der-01/der-03 拆分，非 der-04）。
- **worksheet 孤儿 `origin.r3.*`**：上方第 1 节与 W5 小节记"14 个孤儿 claim"，系 R3 审阅阶段口头计数。**本次实测：27 条 `origin.r3.*` legacy_origin 条目 → 19 个不同 destination claim ID**；该 19 个 ID 与任何产物零重合（既不在 worksheet 30 个 source_unit 的 claims 中，也不在 r3-revision/documents 的 41 条 claim 中），且每条在同一 source_unit 内都存在同 `origin_locator` 的有效正常项 → 判定为**草稿层重复残留**，按 claim_authority（doc 为权威版）降级为噪声、不进入候选管线。清单见 `corrections_20260912.worksheet_orphan_origin_measured`。
- **两份账目对账基线统一为 22**：口径 = **R3 新增 claim 数**。三向核对：① r3 documents 41 claim = r2 保留 19 + r3 新增 22；② worksheet `validation_summary.new_claims` = 22，action_map 23 条动作去重引用 = 22；③ SUPERSEDED-MAP 的 9 条属 **r2 侧被取代项**，与 r3 侧新增 22 为不同口径，**不对冲、不相加**。账目落 `corrections_20260912.reconciliation_baseline_unified`。

### 7.2 W6 闭合复验结果

工具：`tools-r3/validate-authoring-closure.ps1`（14 项检查，只读，可重跑）。
Studio CLI 结构校验 `0 diagnostics`。
闭合复验 **VERDICT = `STRUCTURALLY_CLOSED_OPEN_ITEMS`，PASS 12 / FAIL 0 / OPEN 2**。

**实测计数**：12 档 / assertion 40 / expression 40 / grants 40 / R3 claim 41。

### 7.3 口径订正：expression 16 → 实测 40

上方第 6 节修订 7 记"预期 expression 计数 16（layer 标注 span 数）"，`PROJECTION-MANIFEST-20260912.json` 的 `expected_counts.expressions` 亦写 16；**W2 实交 40**。属**文档未跟上实际**，非实施缺陷。分解：

- 40 = R3 `target_spans` 中**带 `layer` 标注的 16 条**（即计划口径） **+** 中立内核 assertion 按 authoring.v1『`expressions` 必填』**补挂的 summary 档 24 条**。
- `awake.worldbook.authoring.v1.schema.json`：`assertion.expressions` 必填但**无 `minItems`**；`expression.layer` 枚举 rumor/summary/detail/secret。故 **40 与 16 在 schema 下均合法**。
- 原写数值不改写；口径以 `corrections_20260912.expression_count_reconciliation` 与 manifest 同源修正节为准。

### 7.4 遗留未实现：W4 审查 P1-3（referral 接线）

上方第 6 节修订 3 要求"每个表达段落 `fallback_referral_ids` 指向同簇兄弟档，referral ID 命名 `referral.cluster.<place>.<seq>` 并登记进 referral-registry"。**本批未实施**：referral 登记表仅 4 条通用 referral、无 `referral.cluster.*`，12 档产物 `fallback_referral_ids` 覆盖 **0/40**。
补做将改动 12 档产物 + referral 登记表，**登记表哈希（`6E17075F…`）变更会触发全部 12 档 registry_bindings 重绑**。属**待裁定项**，账目落 `corrections_20260912.w4_p1_3_referral_gap`。

### 7.5 待裁定项（OPEN）

| ID | 事项 | 候选 |
|---|---|---|
| C13 | place_cluster → `fallback_referral_ids` 接线 | 补做 / 挂账（并定归属批次） |
| C14 | expression 计数口径 | (a) 维持 40；(b) 收严为 16 |

### 7.6 证据

- `AWAKE\docs\evidence\AWAKE-WORLDBOOK-AUTHORING-PROJECTION-E2-20260912.md`（E2 证据报告）
- `AWAKE\docs\evidence\authoring-closure-20260912.json`（机读闭合证据，该目录 gitignore）
- 本批产物清单与提交范围见同目录 `STATUS-20260912.md`。

## 8. C13/C14 裁定与 C13 阻断判定（2026-09-12）

> 追加记录，不改写上方第 1～7 节原写内容。凡与本节冲突，以本节为准。

### 8.1 C14 裁定：expression 计数维持 40

裁定：**维持 40**（不收严为 16）。

理由：40 条中有 24 条是中立内核补挂的 summary 档，其文本与对应断言正文**逐字一致**——说明它不是"多写一句文案"，而是**授予可见性的载体**。
`awake.worldbook.authoring.v1.schema.json` 中 `assertion.expressions` 为必填但无 `minItems`：断言不因无表达段而失效，但**无表达段即对任何身份不可达**。若中立内核一条表达段都不挂，分层机制就失去地基，与"知识要分层、各身份层次分明"的用户定则相冲突。

- 第 6 节修订 7 与 `PROJECTION-MANIFEST.expected_counts.expressions` 原写"预期 16"，**保留不改写**；口径以本节与 `corrections_20260912.expression_count_ruling` 为准。
- 闭合复验 **C14 由 OPEN 转 PASS**。

### 8.2 C13 补做前调查：为什么不能在世界书侧单干

第 6 节修订 3 要求"每个表达段落 `fallback_referral_ids` 指向同簇兄弟档"。原判"补做将改 12 档 + 登记表并触发重绑"，只算了**世界书侧**的成本。实测查证后又查出三条阻断，且都在本侧之外：

| # | 阻断事实 | 证据位置 |
|---|---|---|
| 1 | 校验器强制 referral 必须命中登记表、且目标须公开可问 | `tools/worldbook-studio/src/Awake.WorldbookStudio.Core/Application.cs:820-821`（`WB-REFERRAL-001` 不存在 / `WB-REFERRAL-002` 未开放公开询问） |
| 2 | 登记表被 Studio golden 测试**钉死为冻结输入**，改字节即测试失败 | `tools/worldbook-studio/tests/Awake.WorldbookStudio.Tests/Program.cs:743`（断言文件 SHA-256 == `6E17075F…`，失败信息 "referral registry golden hash drifted"）；`tests/fixtures/a3-1-authoring-template-golden.v1.json`（`registry.referral_hash` 与 `$.registry_bindings.referral_registry_hash`）、`a3-2-content-graph-golden.v1.json`、`a3-3-preview-golden.v1.json`（多处 `version 1.0.0` + 该哈希内嵌于 envelope/compiled 断言） |
| 3 | 改哈希触发全部 12 档 registry_bindings 重绑 | `Application.cs:742`（`WB-REGISTRY-001`） |

再叠加两条既有约束：golden 依 A3.1/A3.2 计划约定**不得由当前实现运行时重生成、须独立人工确认**；Studio C# 测试代码已被本计划第 2 节"明确不做"排除。

**结论**：C13 不是世界书侧的小补丁，而是**跨线联合批次** —— 须由 Studio/主干侧改登记表、重签三份 golden，再由本侧重跑保存链完成 12 档重绑。

### 8.3 C13 处置与已备交付物

- **处置**：本侧不再挂"待办"，改挂 **BLOCKED（跨线依赖）**。闭合复验 C13 由 OPEN 转 **BLOCKED**，证据完整留档，**不降级为 PASS**。
- **已备交付物**：`projection/CLUSTER-REFERRAL-MAP-20260912.json`（`status: prepared_not_wired`）
  - 5 簇 / 12 条 `referral.cluster.<place>.<seq>` 命名 + 逐档 `fallback_by_doc` 映射，可直接供 Studio 侧批次消费；
  - 由闭合复验 **C15** 独立校验：命名合规、簇归属与 R3 `place_cluster` 一致、逐档 fallback = 同簇兄弟档且不含自身。
- **归属批次**：建议随 Studio 侧下一次 golden 基线变更一并做；若另行指定批次，以指定为准。

### 8.4 本轮复验结果

`tools-r3/validate-authoring-closure.ps1`（新增 C15 交付物校验、C13 改 BLOCKED 状态、C14 转 PASS）：
**PASS 14 / FAIL 0 / OPEN 0 / BLOCKED 1**，VERDICT `STRUCTURALLY_CLOSED_OPEN_ITEMS`；实测 12 档 / assertion 40 / expression 40 / grants 40。

