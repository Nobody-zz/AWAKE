# Worldbook Studio AI 生成链路：提案规则与经验沉淀（backlog）

> 日期：2026-09-09  
> 状态：`draft_backlog`——本文只记录“提案层”规则与经验，不改变任何编辑器代码、schema、taxonomy 或世界书内容。  
> 目的：把会话中拟出的新规则先落盘，后续逐步验证、择优并入 Worldbook Studio，避免提案与编辑器既有规则混淆。  
> 关联文档：`WORLDBOOKSTUDIO-AUTHOR-HANDBOOK-v3-20260910.md`（唯一权威作者手册，2026-09-10 合并写作标准/操作手册/样例/验收）。
> 前身（已并入 v3）：`WORLDBOOK-CALRADIA-KNOWLEDGE-INDEX-V2-20260909.md`（写作标准）、`pravend-codex-reference.md`（参考簇验收说明）。

## 0. 规则分层（防止混淆）

```text
L1 编辑器既有规则：代码 + JSON Schema + taxonomy 中实际强制的内容（真实，只读，未改）
L2 作者产出规范：v2 索引 + pravend-cluster 参考簇（手写标准/参照物，非代码强制）
L3 提案规则：本文登记的新规则与验收工具（当前均未在编辑器生效）
```

**转正门槛（L3 → L1）**：必须满足 ① 有代码实现；② 有自动化测试；③ 有真实生成/校验证据；④ 经用户批准。  
未转正的 L3 提案不得在交付结论中宣称为“编辑器能力”。

## 1. 提案清单

### R1 quote 必须在来源文件中可定位（含子引文）

- 理由：现在 authoring validate 只校验 `quote_hash` 与 quote 文本自洽，不校验引文是否真的来自 locator 文件；AI 生成内容存在“编造看似合理引文”的风险。
- 现状证据：`ValidationServices.cs:116-119`（仅 quote_hash 自洽检查）；`SourceEvidenceMatcher.cs` 已有 `AreEquivalent` / `TryFind`（NFKC + 规范折迭匹配），但未接入 authoring validate 路径。
- 落地建议：把每个 sourceRef.quote（含子引文）对 `authoring/sources/<locator_root>` 文件内容做 verbatim/规范化包含检查，失败报 `WB-SOURCE-*`。
- 验收：pravend-cluster 三条文档的所有 quote 通过；人为改掉一个字即失败。
- 落地实现（2026-09-09）：`ValidationServices.cs` `ValidateRefs` 在 `locatorRoot` 文件存在且 hash 一致后，用 `SourceEvidenceMatcher.TryFind` 校验 quote 必须能在来源文件中定位，否则报 `WB-SOURCE-001`“引文 quote 未能在来源文件中定位”。
- 测试：`Awake.WorldbookStudio.EditorContent.Tests` 新增 `R1 quote must be locatable inside source file`（把 quote 换成来源文件不存在的文本 → `validate` 报 `WB-SOURCE-001`），套件 `8/8`；pravend-cluster 实测所有 quote 逐字命中，`Valid=true`。
- 状态：`implemented`（已进入编辑器强制校验路径）。

### R2 expected-facts 机器可读 + AI 输出自动对照

- 理由：`pravend-codex-reference.md` 的验收表是给人看的；AI 生成结果与参考簇的对照无法自动回归。
- 落地建议：新增 machine-readable expected-facts（文档 id、断言 id、text、kind、era、domain、source locator、quote_hash、必须为空的项：年份/新人物/新战争/NPC 表达），配套“AI 候选 vs expected”审计脚本，输出逐项 PASS/FAIL。
- 状态：`proposed`（未实现）。

### R12 runtime 包携带 settlement 锚点（2026-09-10 完成）

- **实现**：`RuntimePackageCompiler.BuildEntry` 把 authoring 文档 `entity_ids` 规范化为
  `awake:<kind>:<code>`（kind 白名单 `hero|clan|settlement`）写入 runtime 条目扩展 `extensions.entityRefs`；
  未识别的 kind 抛 `WB-DOC-003` 编译期 fail-closed，不静默丢弃。
- **测试**：`EditorContent.Tests` 新增 `runtime package carries settlement anchors`（正/负双向），套件 11/11 PASS。
- **证据**：`scripts/runtime-anchor-evidence.ps1` 用真实本地 Worker 建档文档（`doc.politics.entry-b4b7318c…`）
  走 register→select→approve→compile-proof→compile，runtime 包 `entityRefs` 实测含
  `awake:settlement:town_v3` 与 `awake:settlement:town_v7`，连续两次运行 manifest 哈希一致；
  证据文件 `docs/evidence/WORLDBOOKSTUDIO-RUNTIME-ANCHOR-EVIDENCE.json`。
- **排障要点**：CLI 公开投影使用驼峰字段（`selectionId`/`approvalId`/`compileProofId`）；
  `compile --out` 只能落在工作区 `compiled/` 内；编译后不得删除产物，否则权威层判定
  `WB-AUTHORITY-RECOVERY-409` 并 quarantine 该 operation。
- **边界（重要）**：本批只证明“运行包已携带锚点”。**AWAKE 模组本体运行期读取并消费这些锚点尚未验证**，
  属模组本体闭环范畴，不得据本批声明“游戏内已消费”。
- 状态：`implemented`（编辑器侧）；runtime 消费侧 `proposed`。

### R11 作者闭环验收 + 可复制性验证（2026-09-10 完成）

- **作者闭环验收**：新增 `scripts/author-loop-acceptance.ps1`（打包版 dev 模式）：打开 AI 档案 → 读取原文 → 作者编辑保存（CAS，revision 3→4）→ 保存后校验（`Valid=true`、0 诊断）；编译/导出由 `scripts/a1-authority-smoke.ps1` 覆盖 PASS。报告：`WORLDBOOKSTUDIO-AUTHOR-LOOP-ACCEPTANCE-20260910.md`。
- **首轮摩擦点已修**：①worker 现在为每个 section 产出 `summary` 与 `subdomain`（并带按领域兜底），建档摘要不再是占位；②模型偶尔照抄原句时，worker 改为“官方记载：…”的引用式复述（`text-is-restatement-not-copy` 恢复通过）。
- **可复制性验证**：以第二个官方聚落「加伦/Galend（town_V5）」复跑同一流程（仅换来源与期望事实，无新代码路径）：期望事实 3/3、禁止项 4/4（含跨例污染检查）、状态与证据 6/6、锚点 `entity.settlement.town_v5` 解析通过 → **15/15 PASS**；帕拉汶德回归仍 **19/19 PASS**。报告：`WORLDBOOKSTUDIO-REPRODUCIBILITY-GALEND-20260910.md`。
- **脚本参数化**：`scripts/real-worker-pravend-smoke.ps1` 支持 `-CaseName/-SourceFile/-EvidencePath`；`scripts/pravend-expected-facts-check.ps1` 支持 `-ClusterPath/-EvidencePath`。

### R10 收尾批次（2026-09-10 完成）

- **UI 时期控件**：核查后确认作者表单**早已有**时期控件（`#era-preset` / `#era-certainty` / `#era-start` / `#era-end`，选项来自 catalog.eraPresets），无需新增；AI 候选的 era 会自动带入建档文档并在作者表单显示。
- **历史 generation 清理**：`docs/mappings/persona-entity/generations` 由 13 个精简为 1 个（仅保留 pointer 指向的当前版本）；清理前确认源码/脚本/文档无旧 build id 引用。
- **DLC 家族可见性**：缺中文名的官方 DLC 家族（clan_nord_4..9）改为回退显示代码名并保留“未安装”标签，编辑器可见家族由 76 → **82**，与 counts 对齐。
- **英雄同名标注**：投影新增 `duplicateName` 标记（同名实体），编辑器卡片/明细显示“同名”提示；实测查士丁娜 ×2 命中。
- **验证**：`docs/mappings` 审计全绿（393 地点 / 0 缺字段 / 跨引用全解析 / 仅 1 个 generation）；全量 `scripts\test.ps1` 通过；打包 TEST/CONTRACT PASS；打包版 catalog 可见 415 人物 / 82 家族 / 393 地点。

### R8 生成结果携带地点锚点（entity.settlement.*）

- 落地实现（2026-09-10）：草稿 metadata 增加 `entity_ids`（normalizer 白名单 + 解析 + 序列化）；模板工厂/建档三函数增加 `entityIds`；建档端点按 `候选 metadata.entity_ids` 写入文档 `entity_ids`。
- worker 侧：读取 `docs/mappings/persona-entity` 的地点映射，按 display_name_zh 命中候选事实（text+quote）后附加 `entity.settlement.*`。
- 验收：`expected-facts.v1.json` 增加 `expected_anchors`；`scripts/pravend-expected-facts-check.ps1` 校验锚点出现且能在映射中解析 → 当前 **18/18 PASS**。
- 别名匹配（2026-09-10 已落地）：新增 `docs/mappings/settlement-aliases.v1.json`（依据官方中英文描述中的旧称，例如 `town_V3` → 巴拉维诺斯 / Paravenos）；schema 支持 `aliases`；生成器写入实体；Core 公开投影暴露 `aliases`；worker 锚点匹配同时使用别名。实测：提及“巴拉维诺斯”的候选现在都能拿到 `entity.settlement.town_v3`。
- 遗留 2：参考簇原用的 `entity.settlement.pravend` 已统一替换为映射真 ID `entity.settlement.town_v3`。

### R9 AI 建档后的文档在回读时 schema 校验失败（已修）

- 现象（2026-09-10 实测）：通过“生成 → 采纳 → create-document”落成的文档，在 `/api/editor-document` 回读时返回大量 `WB-SCHEMA-001` 子错误，涉及 `author_created`、`provenance`、`sources` 缺失、以及 `canon`/`game_state`/`source_only`/`approved` 等 enum/const 分支。
- 根因：`SafeYamlLoader.ConvertScalar` 把**空标量**（`coverage:`）解析成空字符串，而 schema 要求 `object|null` → `provenance.coverage` 校验失败并连锁出几十条错误。创建时校验的是内存对象（null 合法），回读时经过 YAML 往返才暴露，所以“两条路径不一致”。
- 修复：空标量按 YAML 语义解析为 null；带引号的空串（`''`）仍保持空字符串。新增确定性测试 `empty YAML scalar loads as null`。
- 验证：真实本地 Worker 生成 → 建档 → 回读 **诊断数 = 0**；全量 `test.ps1` 通过。
- 附带：`WORLD_BOOK_CLOUD_TIMEOUT_SECONDS` 上限由 300s 放宽到 600s，减少本地慢模型的超时抖动。
- 状态：`fixed`。

### R7 era（时期）贯通草稿与建档链路

- 问题：`AuthoringTemplateFactory` 把 `era.key` 硬编码为 `current`，且 `DraftCreateDocumentRequest` / `CreateDocument` / `CreateGeneratedDocumentFromDraft` / 草稿 metadata 都不带 era；历史内容建档后会被错标为 current，违反 v2“历史不与现状混层”。
- 落地实现（2026-09-10）：草稿 metadata 增加 `era`（normalizer 白名单 + 解析都放行）；`CreateGeneratedDocument` / `CreateDocument` / `CreateGeneratedDocumentFromDraft` / 模板工厂增加 `eraKey` 参数；`DraftCreateDocumentRequest` / `NewDocumentRequest` 增加 `Era`，端点按 `request.Era ?? 候选 metadata.era` 生效。
- 测试：`EditorContent.Tests` 新增 `era flows into created documents`（显式 historical 必须落成 historical，缺省仍为 current），套件 9/9。
- 实测：真实本地模型生成的 5 条候选中 4 条 `era=historical`、1 条 `era=current`（现名）；建档回读 `document_era=historical`。
- 遗留：编辑器 UI 仍无“时期”控件（作者无法手动选择/修改，只能用 AI 候选带来的值）——留待 UI 批次。

### R3 Quick Authoring 红队九项覆盖审计与补齐

九项目标（来自本会话任务规格第 9 条）与当前测试证据：

| # | 红队项 | 现状 | 证据位置 |
|---|---|---|---|
| 1 | source 含提示词注入，不得进入 system prompt | 已审计：有测试 | Draft.Tests “source prompt injection remains untrusted user data”（`Program.cs:613-632`） |
| 2 | rumor 不得变 fact | 已审计：有测试（blocking `semantic-rumor-upgrade`） | Draft.Tests “semantic source boundaries block epistemic time and quote drift”（`Program.cs:1764`） |
| 3 | historical 不得变 current | 已审计：有测试（blocking `semantic-historical-current`） | 同上（`Program.cs:1765`） |
| 4 | 多文化/多视角不得被抹平 | 已审计：有测试 | Draft.Tests “multiple requested perspectives cannot collapse to one expression” + “quick authoring rejects unrequested expressions” |
| 5 | quote 可定位但 proposition 超出 quote → 阻断 | 已审计：有测试 + 新增现实用例 | Draft.Tests “semantic source boundaries …” 内 `semantic-claim-exceeds-quote-*` / `semantic-target-exceeds-quote-*`；新增 “redteam claim drift beyond real frigyon quote is blocking” |
| 6 | source claim 少于正文命题 → 失败 | 已审计：有测试（blocking `coverage-proposition-overflow`，create 抛 422） | Draft.Tests “coverage blocks propositions beyond source claims”（`Program.cs:1533`） |
| 7 | unknown content tier 不得自动变 base | 已审计：有测试（wire 保留 unknown） | Draft.Tests “draft content tier never defaults unknown to base”（`Program.cs:610`） |
| 8 | must_not_invent 阻止新增人物/年份/战争/正式 entity ID | 已审计：有测试 + 新增现实用例 | Draft.Tests “must-not-invent constraints block semantic expansions”（`Program.cs:1594`）+ “edited text outside evidence or must-not-invent constraints is blocked”；新增 “redteam must-not-invent blocks realistic pravend fabrication” |
| 9 | retry 不得产生重复候选 | 已审计：有测试 | Draft.Tests “quick authoring retry reuses the frozen semantic packet”（settle 一个 candidate identity）+ “candidate lifecycle rejects duplicate candidate identities” |

- 审计结论：九项全部有对应阻断测试，未发现实现缺口。早期表中“待审计/补齐”标记不准确，已更正。
- 迭代补齐（2026-09-09）：新增两则帕拉汶德现实形状对抗测试（真实中文名/年份/战争/entity ID、真实句子引文漂移），Draft.Tests 由 79 → 81 项全过。
- 状态：`verified`（单元层）；仍需真实本地 Worker 一次端到端生成来验证编排层行为一致。

### R4 官方素材补登：巴拉维诺斯口语用法的 companion 文本

- 素材：SandBox `std_companion_strings_xml-zho-CN.xml:178`（id `RsCPQ167`）：佣兵口述“父母逃难到巴拉维诺斯、帮商人跑腿走私、绕过城门检查”。
- 价值：官方文本证明旧称巴拉维诺斯仍在口语/回忆场景存活；可支撑 commoner/merchant 表达层（P1），不属于 geography 简介内容。
- 归属裁定：奥斯里克相关 lore（`std_settlements_xml-zho-CN.xml:10` 与 SandBoxCore `std_spkingdoms_xml-zho-CN.xml:14`、`std_world_lore_strings_xml-zho-CN.xml:40`，同一字符串 id `79R6gi4l`）是加伦（Galend）视角，归人物/文化线，不进帕拉汶德簇；`std_spcultures_xml-zho-CN.xml:236`（id `DBHAFW5k`）仅为名称 token，价值低。
- 状态：`proposed`（素材已定位，未登记入 source registry）。

### R5 entity.settlement 命名空间的运行时注册（独立批次）

- 现状：`entity.settlement.pravend` 只是文档级锚点；schema/registry 目前只接受 `hero|clan`，运行时 Entity Registry 不消费聚落实体。
- 落地建议：单独排批次设计 settlement 命名空间、注册与条件引用（`awake:settlement:...`），不与参考簇/生成链路混做。
- 状态：`backlog`。

### R6（远期想法）候选分拆与知识分条引导

- 想法：编辑器从一段多时间层官方文本生成时，按“主题簇 → 文档拆分建议（地理/政治沿革/传承…）”给出草稿分案，而不是默认产出单条巨型简介。
- 注意：v2 拆分原则是写作标准（L2），编辑器目前不会自动执行；此条仅是远期想法，不做承诺。
- 状态：`idea`。

## 2. 经验沉淀（本轮实测教训）

1. **quote hash 自洽 ≠ 证据真实**：校验只保证 hash 与引文文本一致，不保证引文出自 locator 文件；引用链必须加 containment 检查才能防“编造引文”。
2. **一段官方文本可含多时间层多主题**：帕拉汶德行 23 整段含建城/首都/经济/易主/传承/改名，若塞进单一 geography intro 会抹平时间层；拆文档时引文粒度必须细于断言（行 23 已拆为 `59c8b208…`、`8888a1ae…`、`af94efeb…` 等子引文）。
3. **现名/旧称 ≠ 死词**：canon 现名是帕拉汶德，但官方 companion 口述仍用“巴拉维诺斯”；表达层设计需保留别名口语存活，不能只按 canon 名归一。
4. **同一 lore 字符串跨 CN 文件重复**（id `79R6gi4l` 在 settlements:10 / spkingdoms:14 / world_lore:40 重复）：登记 source 时以“模块路径 + string id”为准去重，避免同一引文登记多条 registry。
5. **手工验收目标 = 模拟目标**：参考簇的 7 条预期事实是手写参照物，不是编辑器真实生成记录；“AI 生成已验证”只能由真实本地 Worker 的一次生成 + 对照审计来背书，在此之前不得宣称。
6. **文档级锚点与运行时注册分离**：authoring 文档可以引用 `entity.settlement.pravend` 组织内容，但这不代表运行时已能消费；两者混谈会导致交付误判。

## 3. 尚未完成 / 下一步

- [x] R3 九项覆盖审计（2026-09-09）：全部有对应测试，新增两则帕拉汶德现实对抗用例（Draft.Tests 81/81）。
- [x] 真实 Quick Authoring 生成（本地 Worker，只进 `needs_review`）：R9/R10/R11 已记录实测。
- [x] AI 输出 vs expected-facts 对照审计：帕拉汶德 19/19、加伦 15/15（R11）。
- [x] R1 接入 validate 路径并回归 pravend-cluster。
- [x] 编辑器代码改动后重打包本地测试包 + 离线冒烟（TEST/CONTRACT PASS）。
- [x] R12 runtime 锚点贯通（编辑器侧）；runtime 消费侧待模组本体。

## 4. 证据附录

- 参考簇：`tools/worldbook-studio/tests/fixtures/official-reference/pravend-cluster/`（三 authoring 文档 + 双 source registry + 摘录 txt）。
- 摘录 txt SHA-256：`3f63df87be441cfd7ea4eeedb6ca2a0bea050b848170ac65e9696d1753c8df9c`。
- 校验结果：CLI `validate` → `Valid=true`，`Diagnostics=[]`（脚本 `tools/worldbook-studio/_tmp/validate-pravend-reference.ps1`）。
- 官方 CN 文件：SandBox `std_settlements_xml-zho-CN.xml`（SHA-256 `46df7c77…`）行 23、259；SandBox `std_companion_strings_xml-zho-CN.xml` 行 178（id `RsCPQ167`）。
- 硬边界执行情况：未启动游戏、未同步游戏目录、未访问真实 Provider/API/Worker/网络服务、未修改真实源目录、迁移候选、世界书正文、AWAKE 模组本体或任何冻结构建产物。

## 5. 边界与原则总口径（检查缺漏后补入，2026-09-09）

> 用途：后续对用户说明与编辑器验收的统一口径。标注层别：L1=编辑器代码强制；L2=作者写作规范；L3=提案（未生效）。

### 5.1 模式与范围边界

- **两模式分离（L1）**：Quick Authoring 与 Semantic Migration 是两条链路，QA 不得冒充高保真语义迁移；本参考簇（pravend-cluster）只用于 QA 链路验收，语义迁移属另一会话范围。
- **UI 入口（现状约束，非本会话改动）**：保留清晰的“简单提示词 → 完整文件”QA 入口，同时不删除现有分阶段流程（legacy + quick 并存）；当前会话不改 UI，UI 渲染完整性属另一会话核对项。
- **候选数据完整性（协议层 L1）**：strict envelope 强制候选含 facts / metadata / expressions / warnings / unresolved / coverage / target_spans 等字段，缺任一即格式失败；候选必须可完整呈现 evidence 与诊断。

### 5.2 身份、成人内容与视角

- **成人 gate（L1）**：18+ 校验先于一切；`adult_optional` 需要显式确认身份；base 请求拒绝 adult flag；base 生成后不可升级 adult tier。
- **未请求视角不得产出表达（L1）**：`WB-AI-DRAFT-EXPRESSIONS-422`——未请求身份视角时，结果与候选均不得含任何身份表达。
- **表达必须显式授权（L2）**：每条 expression 必须带 `profile_id / scope / min_detail` grants，必要时 denies；不写“三种身份表达”而不指定 profile 与 layer。

### 5.3 输入、证据与生成前校验

- **输入预算（L1）**：source 超长或任一独立输入字段超预算即拒绝。
- **prepare 前置校验（L1）**：必须有 goal 与显式 content tier；请求视角 ≤3；无效输入在配置 Provider 之前即阻断。
- **证据两分法（L1）**：可定位证据才自动 `verified`；不可定位证据保留给人工复核，不静默标记通过；strict Quick Authoring 中 evidence 必须与服务端 source_origin 完全一致（reference_id / locator / quote / quote_hash / evidence_verified），不符直接 422。
- **失败关闭（L1）**：宁阻断不静默修正——strict envelope 不完整、未知字段、解释字段无效、claim 超 quote 均失败关闭。

### 5.4 完整性、审计与指纹

- **请求字段进指纹与审计（L1）**：Quick Authoring 意图字段（mode/goal/instruction/domain/perspectives/style/must_preserve/must_not_invent/tier 等）必须进入 request hash、candidate fingerprint 与审计记录；fingerprint 可独立重算。
- **prompt 版本是契约（L1）**：authoring prompt 修订号参与 batch cache 键，改 prompt 即失效相关缓存。
- **review 决策 ledger（L1）**：merge/split/采纳等 review 决策走 ledger，不可绕过；派生候选保留 source span 归属。

### 5.5 编辑与 CAS

- **人工编辑降级证据（L1）**：编辑后的 source-bound 文本变为 `author_modified` 并失去证据确认；source-bound 文档拒绝直接改源。
- **CAS 防并发（L1）**：保存必须带预期 hash/revision，外部更新时拒绝覆盖（409）；create-document 幂等去重。
- **consent/attempt 授权（L1）**：每次生成经 consent + attempt 门控；attempt 未知态可显式 retry，陈旧/未知结果不得静默重放。

### 5.6 受控词表（L1）

- domain / subdomain / era / assertion.kind / expression.layer 等字段由 taxonomy 与注册表约束，不是任意字符串；`current_and_historical` 不作为正式 era 存档值。

### 5.7 提案未生效（L3 保留）

- R2 expected-facts 机器对照；R6 分拆引导想法。仍需转正门槛（代码 + 测试 + 真实证据 + 批准）。
- R5（settlement 锚点）已拆分：**编辑器侧已转正**（R12，运行包携带 `awake:settlement.*`）；
  **runtime 消费侧仍未转正**，需模组本体实现 + 游戏内证据。
