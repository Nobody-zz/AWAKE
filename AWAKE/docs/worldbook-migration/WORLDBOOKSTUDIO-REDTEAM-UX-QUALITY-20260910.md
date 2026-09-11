# Worldbook Studio 红队审计：用户体验 + 生成质量（2026-09-10）

> 性质：**只读红队审计，不修代码**。目标是先把"影响用户使用体验"和"影响生成质量"的问题查全查细。
> 范围：`tools/worldbook-studio` 的 Web 前端（wwwroot）、AI 草稿生成链路（Core）、编辑/保存、启动器与目录服务、相关测试覆盖。
> 方法：静态代码走查 + 本地只读实测（dev 模式启动打包版 Web，仅回环），不改任何代码、不访问云端 Provider、不启动游戏。
> 证据标注：`[实测]` = 本次真实运行测量；`[代码]` = 由代码构造直接推出（未运行复现）；`[推断]` = 需进一步验证的假设。

---

## 0. 结论摘要

| 编号 | 级别 | 一句话 | 影响面 |
|---|---|---|---|
| G1 | 高 | 生成失败（Pass A 阻断）在 UI 上只显示"操作失败。"，服务端算好的具体原因被三层丢弃 | 生成质量 / 体验 |
| G2 | 高 | Pass A→Pass B 只校验"子集"，不校验"覆盖"：命题可在建档前被静默丢弃，UI 也无任何提示 | 生成质量（完整性） |
| G3 | 高 | 候选分割（"分割莫名其妙"）在服务端**没有任何边界信号**，完全由 Pass B 自由决定 | 生成质量（结构性根因） |
| G4 | 高 | 展示用风险启发式把"同一引文支撑不同侧面"判为 `candidate_conflict` 红色，而 UI 把红色写成"存在阻断问题"，后端却不阻断 | 生成质量 / 体验（信息自相矛盾） |
| G5 | 高 | 输出预算默认 3000 tokens（上限 8000），输入上限 80,000 字符：长资料必然截断 → JSON 解析失败 | 生成质量 |
| G6 | 中 | 本地 Worker 链路**不发送系统提示词**，握手也没有 prompt 版本协商：内置提示词对本地链路是否生效无法从 Studio 侧验证 | 生成质量（治理） |
| G7 | 中 | 请求体同时发送 `source_text` 与逐句 `source_origins[].quote`，输入近似 2 倍膨胀 | 生成质量 / 成本 |
| G8 | 中 | 服务端生成的 source origin locator 是合成值 `source unit 0001`，而落库证据 locator 实际由模型写文件名；两边不一致时保存校验会失败 | 生成质量 / 体验 |
| U1 | 高 | 首屏被 `/api/editor-catalog` 阻塞：实测 196–598 ms、215 KB，且与文档列表同一个 `Promise.all` | 体验 |
| U2 | 高 | 右侧实体目录无虚拟化、无输入防抖：每次按键全量重建最多约 890 张卡片，源数据 604.5 KB | 体验 |
| U3 | 中 | 同一个"开始生成"入口在三处文案不一致（含死代码副本已与出货文案分叉） | 体验 |
| U4 | 中 | `draftEnsureQuickFields` / `draftEnsureUserFacade` 是死代码，其中含第二份快速面板 markup | 可维护性 |
| U5 | 中 | 参考资料 80,000 字符上限对用户不可见（无计数器、无 maxlength） | 体验 |
| U6 | 中 | 结果摘要"候选草稿 N 份"在 0 候选时显示 1 份 | 体验（准确性） |
| U7 | 中 | "创建待审核档案"按钮由 6 个条件控制，禁用时不指出缺哪一项 | 体验 |
| U8 | 中 | 可选面板（实体目录）加载失败会导致整个首屏报"工作区连接失败"，文档列表也不渲染 | 体验 / 健壮性 |
| U9 | 低 | 错误码映射未覆盖 `WB-AI-DRAFT-PASS-*` 全系列；且启动器端口占用提示不告诉用户是谁占用 | 体验 |
| U10 | 低 | 时期预设（UI/代码）与作者手册（v2/v3）不一致：手册写 `pre_war/during_war/post_war/long_term`，界面实际是 `before_event/after_event/persistent/custom` | 文档-实现一致性 |
| N1 | 高 | 批量工作台链路完全没有内容分层概念，建档硬编码 `base`，且全链路 0 处 18+/成人确认 | 生成质量 / 合规 |
| N2 | 中 | `canon` 守卫不对称：界面把选项设成 disabled，服务端 `Merge` 却接受任意 status（仅事后 `WB-CANON-001` 才报） | 一致性 |
| N3 | 中 | 归一化阶段的 `Limit()` 静默截断（无省略号/无警告），且发生在 fail-closed 的解析器之前 | 生成质量 |
| N4 | 低-中 | 本地草稿自动保存失败（超 1.5 MB / 存储不可用）被丢弃返回值，静默不再保存 | 体验（可能丢稿） |
| N5 | 低 | 手工新建档案默认 `politics` + `base`，与 AI 路径"层级必须显式选择"口径不一致 | 一致性 |
| N6 | 低 | 启动期批量恢复是同步全量扫描 `batches/` 全部批次 | 体验（启动耗时） |
| B1 | 中-高 | 批量轮询每 1.8 s 全量重建对话框两次：正在输入的"审核说明"被清空、风险级别重置、队列滚动回顶 | 体验 |
| B2 | 低 | 轮询不做页面可见性判断，后台标签页也持续请求 | 体验（资源） |
| B3 | 中 | 兼容分阶段流程预填 9 条"西帝国"示例视角（且两处副本） | 生成质量 |
| B4 | 高 | Persona 交接 API 无调用方、无 UI 入口（PWB 源码也不调用），与三工作站计划的验收项冲突 | 结构 / 未接线 |
| B5 | 中 | "检查整个工作区""身份预览"没有进行中状态，且每次都全量重建快照 | 体验 |
| B6 | 低 | `#toast` / `#batchNotice` 缺 `aria-live`，读屏用户收不到提示 | 无障碍 |
| D1-1 | 高 | 没有任何文档级重复/冲突检测；两条矛盾词条可同时 canon 并一起编译进运行包 | 内容质量 |
| D1-2 | 中 | 一个关键词映射到多条词条时既不消歧也不诊断（`keywordToEntryIds` 多值） | 内容质量 |
| D1-3 | 中 | 运行契约层没有 aliases 概念（只有 keywords + extensions），K1 的修复需先做取舍 | 契约 |
| D2 | 低-中 | 文风只有自由文本、无受控词表；四档文风与本工具 `content_tier` 的归属没有写明 | 产品范围 |
| D3 | 中 | 互斥体按包路径命名 → 两份解压副本可同开同一工作区，冲突只在中途以"另一个实例正写入"暴露 | 体验 / 并发 |
| D4-1 | 中 | 编辑校验只要求"改动后文本是引文子串"，删掉否定词即可绕过，极性漂移不被阻断 | 内容质量 |
| D4-2 | 中 | 建档后 evidence 不再写 `verified`，"作者改过、依据已降级"这一信息在文档层丢失 | 内容质量 |
| D4-3 | 中 | 不保留"AI 原文 ↔ 作者改后"对照，没有漂移度量 → 无法评估生成质量实际被改写多少 | 可评估性 |
| D5 | 中 | UI 不显示版本、不告知日志路径、不显示 `correlation_id`；运行期日志只覆盖权威链错误；无诊断导出 | 可支持性 |
| D6 | 中-高 | 交付包 574.6 MB，其中 175 MB 是"解压树 + 同内容 zip"双份；`UiWorkstation.Adapter.ps1` 写死开发机游戏路径，导致安全护栏在客户机上永远不生效 | 交付 |
| D7 | 低 | 启动器不检测浏览器能力即用系统默认浏览器打开；前端依赖 `<dialog>.showModal()` | 兼容性 |

---

## 1. 生成质量类问题

### G1 生成失败原因在 UI 上被抹掉（高）

链路：`QuickAuthoringOrchestrator.EnsurePassAGate` 抛
`WB-AI-DRAFT-PASS-A-422: Pass A 仍有阻断项，不能进入作者投影：<具体原因>`
（`src/Awake.WorldbookStudio.Web/QuickAuthoringOrchestrator.cs:224-233`）。但：

1. `AiFailure`（`src/Awake.WorldbookStudio.Web/Program.cs:622-643`）只返回 `SafeMessage(code)`，**丢弃 `ex.Message` 里的具体原因**；`401..422` 的 switch 也没有 `WB-AI-DRAFT-PASS-*` 分支。
2. `SafeMessage`（`Program.cs:719` 起）同样没有 `WB-AI-DRAFT-PASS-*` 条目 → 落到默认 `"请求无法完成，请检查档案校验结果。"`。
3. 前端还会再用自己的映射覆盖服务端 message：`safeAiErrorMessage`（`wwwroot/studio-authoring-ux.js:14-35`）没有该码 → 返回 fallback `"操作失败。"`（`studio-authoring-ux.js:83-89`）。

**用户实际看到**：状态行"这次没有生成可用草稿，请根据提示处理后重试。" + 提示"操作失败。"，没有任何"哪一条命题无法绑定来源"的信息。
**影响**：这是 Quick Authoring 最常见的失败形态（弱模型无法为某句命题绑定来源），却给出最少信息，用户只能盲目重试或换模型 —— 直接对应"生成质量差/体验差"的体感。
**注**：同一问题也影响 `WB-AI-DRAFT-PASS-B-422`、`WB-AI-DRAFT-PASS-A-CAS-409`、`WB-AI-DRAFT-PASS-A-400` 等（`QuickAuthoringSemanticPacketFactory.cs:17-34,86-97,180,210-230`）。
等级依据：`[代码]`（映射表逐条核对，无该前缀条目）。

### G2 语义完整性只做"子集"校验，命题可被静默丢弃（高）

- Pass B 校验只要求 **candidate 里的 proposition/claim/target_span 是冻结 packet 的子集**
  （`QuickAuthoringSemanticPacketFactory.cs:200-232` `EnsureSubset`）。
- 同时要求 **顶层** graph 与 packet 完全一致（`EnsureExactGraph`，同文件 `:210-214`），但建档用的是**候选级** facts（`wwwroot/studio-draft.js:651-656` `draftSelectCandidate`），顶层 graph 不参与建档。
- 归一化阶段只检查"每个候选至少有 1 条 proposition/claim/target_span"
  （`src/Awake.WorldbookStudio.Core/AuthoringDraftResponseNormalizer.cs:715-762` `EnsureQuickSemanticCompleteness`）。
- 因此：**没有任何地方要求候选并集覆盖 packet**；20 条命题只投影 1 条也能通过全部闸门。
- UI 侧同样无信号：结果摘要只显示"资料内容 N 条 / 可定位来源 N 条 / 警告 N 条"，没有"未覆盖命题数"（`wwwroot/studio-draft.js:595-617` `draftRenderUserResultSummary`）。

**影响**：长资料"看起来生成了，其实丢了一半内容"，而作者无从察觉 —— 比报错更危险。
等级依据：`[代码]`；未构造端到端用例复现（属未验证项）。

### G3 分割没有服务端边界信号（高，"分割莫名其妙"的结构性根因）

- Pass A 提示词明确要求模型"5. 决定候选边界"（`AuthoringDraftPromptCatalog.cs` `SemanticWorkflow`）。
- 但 Pass A 产出的 packet 结构**不含任何候选/边界字段**：只有
  `facts / expressions / propositions / claims / target_spans / unresolved / coverage`
  （`QuickAuthoringSemanticPacketFactory.cs:49-66`）。
- Pass B 提示词要求"只能从 semantic_packet 投影……和候选正文"，却没有可依赖的边界输入，于是**分组实际由 Pass B 自行决定**。
- 也没有候选数量上限：`ValidateProjection` 不检查 `candidates.Count`，解析层上限是 64（`AuthoringDraftContracts.cs` `MaxArrayItems = 64`）。

**影响**：合并/拆分的自由度全在模型侧，同一份资料换一次运行就可能得到不同切分；多候选时作者要逐条审阅 → "审查冗长"。
等级依据：`[代码]`。

### G4 风险启发式会误判为"冲突/阻断"（高）

- `FindConflictCandidateIds`：把 **证据键（reference_id+locator+quote_hash）相同、但"候选第一条事实文本"不同** 的候选判为冲突
  （`src/Awake.WorldbookStudio.Core/AuthoringReviewProjection.cs` `FindConflictCandidateIds`），随后 `risk=red`、`reason=candidate_conflict`。
- 只要同一句引文支撑两个不同侧面（正是参考簇鼓励的拆分方式），就会被判红。
- 后端并不因此阻断（`EnsureCandidateCanBeCreated` 只看 `blocking`，`AuthoringDraftEndpoints.cs:355-368`），但 UI 文案把红色描述为 **"存在阻断问题"**（`wwwroot/studio-draft.js` `draftCandidateDetailMarkup` 的文案表）。

**影响**：合法拆被标红并提示"阻断"，作者被引导去做无意义的返工；同时真正的阻断项被"狼来了"淹没。
等级依据：`[代码]`；现有测试只覆盖"同引文 + 另一种解释"这一冲突用例（`tests/Awake.WorldbookStudio.Draft.Tests/Program.cs:3123` 起），**没有覆盖"同引文 + 合法不同侧面"**，因此不构成反证。

### G5 输入/输出预算失衡（高）

| 项 | 值 | 位置 |
|---|---|---|
| 参考资料上限 | 80,000 字符 | `AuthoringDraftContracts.cs:421,454` |
| 输出 tokens 默认 | **3,000** | `AssistanceProviderContracts.cs:45`（`ParseBoundedInt(..., 3000, 128, 8000)`） |
| 输出 tokens 上限 | 8,000 | 同上 |
| 请求超时默认 | 60 s（范围 5–600） | 同上 |

Pass A 必须输出完整的 propositions/claims/target_spans JSON（长资料可达数百条），3,000 tokens 远远不够 → 输出被截断 → `WB-AI-DRAFT-FORMAT-JSON` 或字段缺失类错误。
**影响**：不调参的默认配置在真实资料量下基本不可用；用户会归因为"AI 不行"，而非"预算配置不对"。
附带命名问题：本地 Worker 复用 `CloudTimeoutSeconds`（`AuthoringDraftProviders.cs:243`），本地慢模型必须去改一个叫 `WORLD_BOOK_CLOUD_*` 的变量，配置语义误导。
等级依据：`[代码]`（默认值读取逐行核对）。

### G6 本地 Worker 链路不发送系统提示词，且无版本协商（中）

- `BuildChatRequest`（云端）会把 `AuthoringDraftPromptCatalog.Build(...)` 作为 system message 发出（`AuthoringDraftContracts.cs:844`）。
- `BuildWorkerRequest`（本地）只发送 `{protocol, worker_id, client_nonce, request: ToWire(request)}`（同文件 `:854-861`），**没有 system prompt**。
- 握手只校验 `protocol / client_nonce / worker_id / timestamp / signature`（`AssistanceProviders.cs:287-310`），**不协商 prompt_revision / normalization_revision**。

**影响**：v5 内置提示词的整轮清洗成果，在本地链路是否真的生效，Studio 侧无法验证也无法拒绝；`coverage` 里回填的 `semantic_packet_prompt_revision` 只是回显请求值，不构成证据。
等级依据：`[代码]`。

### G7 请求体近似 2 倍膨胀（中）

facts / complete 阶段同时发送：

- `source_text`（整篇资料）；
- `source_origins[]`，每个 origin 的 `quote` 是**整句原文**（`AuthoringSourceOriginCatalog.Build` 按 `。！？；` 与换行切句）。

见 `AuthoringDraftContracts.cs` `ToWire`（Facts/Complete 分支）与 `AuthoringSourceOriginCatalog.cs`。
**影响**：上下文被同一份文本占两次，等价于把 G5 的预算问题放大一倍；长资料更容易触发截断。
等级依据：`[代码]`。

### G8 source origin 的 locator 与实际落库证据不一致（中）

- 服务端生成 `Locator = "source unit 0001"`（合成值，`AuthoringSourceOriginCatalog.cs`）；
- 提示词却声明"locator 是服务端提供的来源事实，不由模型决定"；
- 实际落库证据里 locator 是模型写的文件名（参考实现见 `docs/evidence/local-worker-package-20260906-182345/document-readback.json` 中 `locator: pravend-official-cns-extract.txt`）；
- 而 R1 校验要求 quote 能在 `authoring/sources/<locator>` 对应文件中定位（失败报 `WB-SOURCE-001`）。

**影响**：模型若"听话"地照抄 `source unit 0001`，作者保存时就会撞上来源定位失败；两种口径并存是长期隐患。
等级依据：`[代码]` + 既有证据文件对照。

### G9 死提示词常量与现行合同冲突（低）

`AuthoringDraftContracts.cs:793`、`:795` 的 `SystemPrompt` / `CompleteSystemPrompt` **没有任何引用**，且其顶层字段清单（只有 `schema_version…warnings`）与现行 v5 合同（要求 `candidates/propositions/claims/target_spans/unresolved/coverage`）**直接矛盾**。
**影响**：未来任何人"改提示词"时极可能改到这份死文本，无任何效果，还看不出问题。
等级依据：`[代码]`（全仓引用检索，仅定义处出现）。

### G10 摘要占位文本会写进档案（低）

`AuthoringDraftDocumentBuilder.cs:25-28`：summary 为空时写入
`"待补充：请在作者表单中检查并完善这份档案的范围。"`。
模板工厂对新档案也使用 `"待补充：…"`（`AuthoringTemplateFactory.cs:33,50,59`）。
**影响**：占位文本可能被当作正文保存并进入后续编译；作者不易发现。
等级依据：`[代码]`。

### G11 警告被硬截断（低）

`AddEvidenceWarning` 在 `warnings.Count >= 64` 时静默丢弃后续警告（`AuthoringDraftResponseNormalizer.cs:1218-1222`）。
**影响**：长资料下"缺证据/无法定位"的告警只显示前 64 条，用户以为已经看完。
等级依据：`[代码]`。

---

## 2. 使用体验类问题

### U1 首屏被实体目录阻塞（高）[实测]

在打包版 dev 模式（仅回环）实测 `/api/editor-catalog`：

```text
call 1: 598 ms  bytes=220125
call 2: 218 ms  bytes=220125
call 3: 211 ms  bytes=220125
call 4: 196 ms  bytes=220125
call 5: 198 ms  bytes=220125
median = 211 ms
```

原因：该端点每次都重新读取并**做 JSON Schema 校验 + 跨文件 SHA-256 校验** 604.5 KB 的
`docs/mappings/persona-entity/generations/b1-…/entity-registry.v1.json`（`EntityCatalogService.LoadAndValidate`，无缓存）。
而前端把它放进 `bootstrap` 的 `Promise.all`（`wwwroot/index.html:184`），与 `/api/health`、`/api/workspace`、`/api/documents` 同批 → **首屏必须等它**。
**影响**：每次打开/刷新工作室都要多等 0.2–0.6 s 并下载 215 KB；这是"打开就慢"的直接来源。

### U2 右侧目录无虚拟化、无防抖（高）[代码]

- `$("entityCatalogSearch").addEventListener("input", ... renderEntityCatalog())`（`wwwroot/studio-entity-catalog.js:160`）→ 每次按键全量重建；
- 列表用 `innerHTML = entities.map(...).join("")` 一次性铺全部结果（同文件 `:156`），实体总量约 890（415 人物 + 82 家族 + 393 地点）；
- `renderEntitySelection` 还对结果做 `find`（`:119`）。

**影响**：在右侧窄面板里输入搜索词会明显卡顿；与"右侧 UI 太拥挤"的体感直接相关。
建议方向（供后续批次参考，不在本次改动）：输入防抖、结果分页/上限 + "继续加载"、目录按需加载。

### U3 同一入口文案三处不一致（中）[代码]

| 位置 | 文案 |
|---|---|
| 快速面板初始状态（`studio-draft.js:42`） | 准备好后点击**"开始生成草稿"** |
| `draftApplyFacadeMode()` 快速分支（`studio-draft.js:172-190`） | 准备好后点击**"开始整理资料"** |
| 死代码副本（`studio-draft.js:66-77`） | "Quick Authoring 快速创作"、"创作目标"、"简单提示词" |

触发方式：修改"内容范围"下拉即会调用 `draftApplyFacadeMode()`（`studio-draft.js:867`），状态行文案**当场变成另一个说法**。
**影响**：按钮叫"开始生成草稿"、状态行却让点"开始整理资料"，用户会去找一个不存在的按钮。

### U4 死代码：第二份快速面板（中）[代码]

`draftEnsureQuickFields()`（`studio-draft.js:66`）在整个前端**从未被调用**；它内部又调用 `draftEnsureUserFacade()`（`:78`），因此 facade 也从不执行。两者合计约 90 行，且内含**完整重复的快速面板 markup**（与出货的 `draftQuickMarkup()` 文案已经分叉，见 U3）。
**影响**：改 UI 文案/字段时极易改到不生效的那一份；也说明快速面板经历了"先做 facade、后重写 markup"的返工，旧版本未清理。

### U5 参考资料长度上限不可见（中）[代码]

`draftSourceText` 没有 `maxlength`，也没有字符计数器（全仓 `maxlength` 检索为空；`draftSourceStats` 仅显示行数/字数统计，不显示上限）。
**影响**：用户粘贴长文后点击生成，等待后才收到"参考资料太长，请先分成几份资料。"；没有事前反馈。

### U6 候选数量显示不准（中）[代码]

`draftRenderUserResultSummary`：`候选草稿 ${candidates||1} 份`（`studio-draft.js:596-617`）。
当 `candidates.length === 0`（模型只给顶层 facts、未形成候选）时显示"1 份"。
**影响**：与实际结果不符，误导作者对结果的判断。

### U7 建档按钮禁用无原因（中）[代码]

`draftCanCreate()` 需要同时满足至少 9 个条件（有 draftId、当前只选 1 个候选、至少一条已采纳事实、标题非空、摘要非空、已选分类、已选内容层级、成人内容已勾选确认、没有阻断项）（`studio-draft.js:643`）。
界面上只有一条整体状态行，**不指出缺哪一项**；`renderDraftBusy` 只是把按钮置灰（`:509`）。
**影响**：典型"点了没反应"，尤其"忘记选内容范围"这一条（在下拉里，容易被忽略）。

### U8 可选面板拖垮整个首屏（中）[代码]

`bootstrap()` 里 `/api/editor-catalog` 与核心接口同批；一旦它抛错（例如 schema 缺失 → `EditorFailure` 400），整个 `try` 进入 `catch`，显示"工作区连接失败"，**文档列表也不渲染**（`wwwroot/index.html:184`）。
而实体目录本身在服务端设计上是"可选、缺少就降级"的能力（`EntityCatalogService` 多处返回 `Unavailable` 并给 warning）。
**影响**：一个附加面板的故障会伪装成"工作区坏了"。

### U9 错误码覆盖与启动器提示（低）[代码]

- `SafeMessage`（`Program.cs:719`）与 `safeAiErrorMessage`（`studio-authoring-ux.js:14-35`）都未覆盖 `WB-AI-DRAFT-PASS-*`、`WB-AI-WORKER-HANDSHAKE-*`、`WB-AI-DRAFT-STATE-*` 等；且 `AiFailure` 主动丢弃服务端已写好的中文原因（见 G1）。
- 启动器：`WB-PORT-409-OTHER` → "本机 5077 端口已被其他程序占用，关闭占用程序后重试。"（`LauncherForm.cs:344-353`），不指明占用者，也不提供"用另一个端口启动"的出路。

### U10 时期预设与作者手册不一致（低）[代码]

- 界面/代码实际预设：`current / historical / before_event / after_event / persistent / unknown / custom`（`AuthoringEditorModel.cs:118`，投影 `ProjectEra` `:589-605`）。
- 手册（v2 索引，以及上一批合并的 v3 手册 B2）写的是 `current / historical / pre_war / during_war / post_war / long_term / unknown`。
- schema 侧 `era.key` 只要求非空字符串（`docs/worldbook-studio-plan/awake.worldbook.authoring.v1.schema.json` 的 `$defs.era`），所以两种写法都能存。

**影响**：作者按手册找"战前/战后"，界面上根本没有这些选项；这是**文档与实现不一致**，并且 v3 手册继承了这条错误，需要在下一次文档批次里修正（或反过来统一预设）。

---

## 3. 测试覆盖缺口（与上述问题对应）

| 缺口 | 说明 |
|---|---|
| 风险启发式 | 仅覆盖"同引文 + 另一种解释"冲突用例（`Draft.Tests:3123` 起），无"同引文 + 合法不同侧面"用例 → G4 可长期存在而无回归保护 |
| 命题覆盖 | 无任何"候选并集是否覆盖 packet"的测试 → G2 |
| 候选边界 | 无"同一输入两次生成边界应稳定"的测试 → G3 |
| 提示词合同 | 云端路径有 prompt 装配测试；本地 worker 路径不发送提示词，也没有"prompt_revision 一致性"断言 → G6 |
| 前端快速面板 | `draftEnsureQuickFields` / `draftEnsureUserFacade` 无测试（因为不可达）→ U4 |
| 首屏性能 | 无任何首屏/目录加载的性能基线 → U1/U2 |

---

## 3.5 第二轮补充（同日复查：批量工作台 / 编辑保存 / 补丁与草稿）

### N1 批量工作台没有内容分层概念，硬编码 `base`（高）

- `BatchDocumentService.cs:224` 在建档调用里把 content tier 写死为 `"base"`：
  `_documents.CreateGeneratedDocumentFromDraft(title, summary, domain, "batch." + itemId, draftFacts, [], "base", "author.developer", candidateSet)`。
- 全链路检索：`BatchDocumentService.cs`、`Batch*.cs`、`BatchEndpoints.cs`、`wwwroot/studio-batch.js` 中 **`adult` / `18+` / `成人` / `content_tier` 出现次数为 0**。

**影响链**：批量工作台处理成人向参考资料时，会静默产出 `base` 档案；这些档案随后可按 base 申请编译授权并进入运行包（`compiled`/`export` 只按 tier 做闭包校验，见 `Application.cs:537-551` 的 `WB-TIER-001/002`）。也就是说：
① 内容分层被静默默认（与"unknown 不得自动变 base"的项目口径相悖）；
② 成人内容的 18+ 显式确认门在批量路径上完全不存在。
等级依据：`[代码]`（未实际跑成人素材，避免副作用）。

### N2 `canon` 守卫不对称：界面禁用，服务端接受（中）

- 作者表单对 `author_created` 档案把 `canon` 选项设为 **disabled**（`wwwroot/index.html:135` → `options(statuses, m.status, m.sourceMode==="author_created"?["canon"]:[])`，`options()` 在第 119 行把第三个参数当 disabled 列表）。
- 但服务端 `AuthoringEditorProjection.Merge` 直接写入 `candidate["status"] = Text(model["status"]) ?? …`（`AuthoringEditorModel.cs:154`），**没有状态白名单**。
- 唯一的拦截是事后校验 `WB-CANON-001`（`Application.cs:749-757`，要求 `author_created.review_status == "approved"`），而它只在 `validate` / 编译快照时触发；编译确实会拒绝无效快照（`Application.cs:551` `if (!snapshot.Report.Valid) return result;`）。

**影响**：磁盘上可以存在"status=canon 但 review_status=draft"的档案（列表里显示为正典），直到有人跑校验才报错。界面意图与服务端强制力不一致。
等级依据：`[代码]`。

### N3 静默截断与"宁阻断不静默修正"原则相悖（中）

- `AuthoringDraftResponseNormalizer.Limit(value, maximum)` 超长直接 `value[..maximum]`，**无省略号、无警告、无 blocking**（`AuthoringDraftResponseNormalizer.cs:1400-1401`）；被用于 `text`（12,000）、`object`（4,000）、`predicate/subject`（512）、`unresolved.message`（2,000）等（同文件 `:458-459,486-495,698-700,1268`）。
- 解析器一侧却是 fail-closed：`Bounded(...)` 超长即 `throw`（`AuthoringDraftContracts.cs`，报 `WB-AI-DRAFT-FORMAT-JSON`）。
- 调用顺序是 **先 Normalize 再 Parse**（`AuthoringDraftProviders.cs:254`：`Parse(Normalize(node, request))`）。

**影响**：模型超长输出会被"削到刚好能过校验"，然后当作完整内容落库；作者看到的是一段被从中间截断的文本，且没有任何标记。这正是项目原则里明确反对的"静默修正"。
等级依据：`[代码]`。

### N4 本地草稿自动保存失败被忽略（低-中）

`draftPersist()` 丢弃了 `draftLocalStore.schedule(...)` 的返回值（`wwwroot/studio-draft.js:291`）。而 `save()` 在 `too-large`（超过 1.5 MB，`studio-local-drafts.js:3,48`）或 `unavailable`（localStorage 不可用/配额满）时返回失败状态。
**影响**：草稿体积过大或浏览器存储不可用时，自动保存**静默停止**，用户只有在刷新后才发现草稿没恢复。
等级依据：`[代码]`。

### N5 手工新建档案默认 `politics` + `base`（低）

`openNew()` 强制 `$("newDomain").value="politics"`、`$("newTier").value="base"`（`wwwroot/index.html:182`），而 `createDocument` 只强制二级主题（`:183`）。
对比 AI 草稿路径要求内容层级必须显式选择（`ResolveDraftContentTier` 对空值与 `unknown` 直接报错）。
**影响**：同一次"内容分层"决策，两条路径口径不一致；手工建档会惯性落在 base。
等级依据：`[代码]`。

### N6 启动期批量恢复是全量同步扫描（低）

`Program.cs:23` 在启动时同步调用 `BatchRecoveryService.RecoverInFlight()`，它遍历 `batches/` 下**所有**批次目录、逐个取写租约并读取 item 文件（`BatchRecoveryService.cs:16-56`）。
**影响**：历史批次多的老工作区，启动时间随批次数线性增长（本次未测；属可优化项）。
等级依据：`[代码]`。

### 本轮核验为"设计正确"的控制点（避免误伤，也供后续批次参照）

这些地方是明确做对的，改动时不要破坏：

1. **AI 补丁白名单**：`JsonPatchEngine.IsAllowedPath` 只放行 `title/<locale>`、`summary/<locale>`、`assertions[i]/text/<locale>`、`assertions[i]/expressions[j]/text/<locale>`（`AssistanceContracts.cs:218-224`），且 `SuggestionStore.Apply` 校验 `documentId + sourceDocumentHash + revision + bufferId + applyNonce + applied` 全等（`SuggestionStore.cs:379-390`）→ AI 建议**无法**改权限、状态、tier 或 ID，与提示词声明一致。
2. **保存原子性**：`Workspace.SaveAuthoring` 先写 `.<guid>.tmp` 再 `File.Move(..., overwrite:true)`（`Workspace.cs:377-382`）。
3. **作者表单不吞未知字段**：`AuthoringEditorProjection.Merge` 以 `Clone(original)` 为基底，只覆盖受控字段（`AuthoringEditorModel.cs:130-164`），并对来源型内容做 `EnsureSourceObjectsUnchanged`。
4. **本地草稿存储**：1.5 MB 上限、30 天过期、密钥字段黑名单、staleness 判定（`studio-local-drafts.js`）。
5. **破坏性操作都有二次确认**：删除事实/表达/权限/推荐对象、放弃未保存修改、切换模式、打开其它档案、丢弃批次。
6. ~~无 `setInterval` 轮询~~ **（2026-09-10 第二轮更正：这条结论错了）** 主编辑器确实没有轮询，但**批量工作台有 1.8 s 的 `setInterval` 轮询**（`studio-batch.js:239-240`），由此产生 B1。`index.html` 的 134 个 DOM id 全部唯一、无重复挂载这一点仍然成立。

---

## 3.6 第三轮补充（批量工作台状态机 / 交接 / 预览 / 无障碍）

### B1 批量轮询每 1.8 s 重建整个对话框，会清空正在输入的审核说明（中-高）

- `poll()` 每 **1800 ms** 触发一次（`studio-batch.js:239-240`），条件是"批次未结束且有项目在跑"。
- 每次 tick 调用的 `refresh()` **在开头就 `renderAll()`**（`:200`），成功后再 `renderAll()` 一次（`:207`）→ 每个 tick 全量重建两次。
- `renderAll()`（`:186`）会重建 `#batchReviewContent`，而其中的**风险级别 `<select>` 与审核说明 `<textarea id="batchReviewerNote">` 都是用 `innerHTML` 现搭的**（`:271`）。

**后果**：正在"事实提取"过程中（`view` 已是 review，右侧可审核已完成的项目），作者在审核说明里打字，**每 1.8 s 被清空一次**；风险级别也会被重置回默认 `green`。同一个 tick 还会重建左侧队列（`:160`），**列表滚动位置回到顶部**。
**触发条件很现实**：批量提取 20 个项目时，先完成的项目可以立刻审核，边审边等是预期用法。
等级依据：`[代码]`（渲染路径逐层核对；未实机跑长批次验证观感）。

### B2 轮询不看页面可见性（低）

`poll()` 没有 `document.hidden` / `visibilitychange` 判断：标签页在后台时仍每 1.8 s 发两次请求（manifest + report），最多持续整个批次运行期。
等级依据：`[代码]`。

### B3 兼容分阶段流程预填了 9 条"西帝国"示例视角（中）

`draftLegacyDefaultPerspectives()`（`studio-draft.js:777`）返回 9 行硬编码视角（"西帝国士兵 / 西帝国将领 / 南、北帝国执政官与贵族 / 西帝国富商家族（瓦罗斯）…"），并在打开兼容流程时写入输入框（`:820`）；同一份 9 行文本在 `draftLegacyMarkup()` 的 textarea 默认值里**又抄了一份**（`:38`）。
作者不手工清空，这些与当前资料无关的视角就会随请求进入生成（`draftSourcePayload` → `requestedPerspectives`）。
**影响**：跨领域污染表达生成；且是明显的演示残留物，两处副本容易长期分叉。
等级依据：`[代码]`。

### B4 Persona 交接 API 没有任何调用方，也没有 UI 入口（高，按项目规则属 P0 类）

- 服务端已完整实现 5 个路由（`WorkstationHandoffEndpoints.cs`：import / accept / consume / recover / get），并有独立测试与循环回环 smoke（`scripts/workstation-handoff-loopback-smoke.ps1`）。
- 但 **`wwwroot` 全目录对 `handoff`/`workstation` 的引用数为 0**（含 index.html 与所有 JS）。
- **Persona Workbench 源码也不调用它**：在 `tools/persona-workbench` 全局检索 `integration/persona/handoff`、`worldbook-studio` 均无命中（仅测试夹具里出现过 `wbs-handoff-` 这样的字符串常量）。
- 唯一调用者是测试脚本 `tools/workstation-integration/integration-smoke.ps1`。
- 而 `docs/PLAN-AWAKE-THREE-WORKSTATIONS-20260904.md` 的验收条件明确要求"**All three workstations expose a discoverable, observable entry point**"，并把 handoff 列在范围内。

**结论**：这是一条"服务端已建成但没有任何真实消费者、也没有可发现入口"的链路。按本工作区硬规则（"新增服务必须同时接入口与调用方；'存在但未调用'按 P0 缺陷处理；未接线的实验内容必须显式标记未启用"），它要么补上入口/消费者，要么显式标注未启用。
等级依据：`[代码]`（跨仓检索 + 计划文档对照）。

### B5 "检查整个工作区"和"身份预览"没有进行中状态（中）

`validateSafe()` 与 `previewSafe()`（`studio-editor-safety.js:277-287`）在 `await` 之前**没有 `state.busy`、没有按钮禁用、没有 `action(...)` 提示**，直接请求然后 toast。
而两者在服务端都会走 `BuildSnapshot()` 全量重建工作区快照（读取 + schema 校验全部文档 + 来源登记哈希），成本随工作区规模线性增长。
**影响**：大工作区上表现为"点了没反应"；用户会重复点击，形成多个并发全量校验。
等级依据：`[代码]`。

### B6 无障碍：关键提示未进读屏通道（低）

- `#toast`（`index.html:124` 的 `toast()` 目标）**没有 `role="status"` / `aria-live`**；而 `#saveStatusMessage` 与 `#draftFlowStatus` 都正确带了 `aria-live="polite"`。
- 批量工作台的 `#batchNotice`（`studio-batch.js:110`）同样没有 aria-live。
- 正面：`role="` 的 5 处全用在 `<dialog>` 上，批量对话框有 `aria-labelledby`、队列复选框有 `aria-label`，且批量对话框显式保存/恢复焦点（`returnFocus`）。
**影响**：以读屏为主的作者会漏掉所有 toast 级反馈（保存成功、导出完成、生成失败原因）。
等级依据：`[代码]`。

### 第三轮核验为"设计正确"的控制点

1. **批量失败分类**：`BatchFailureDetails` 把 12 类错误映射为 outcome/status_reason/中文前缀，并向用户给出"结果暂时无法确认"这类如实措辞，不把未知说成成功。
2. **批量破坏性操作都有确认**（接受/退回/批量接受/暂停/取消/接管），且接管的确认文案明确说明"读取授权会失效"。
3. **授权不落浏览器**：consent token 只存内存，刷新后必须重新确认（UI 也如实说明）——这比本地草稿更保守，是正确的取舍。
4. **交接端点自带边界**：`RequireFields` 拒绝未冻结字段，accept/consume/recover 都要求 receipt_id + expected_status，consume 的 fault injection 只在非生产环境生效（有测试覆盖）。
5. **文本转义**：对 entity-catalog / batch / draft 三处渲染器做了定向扫描（`${...}` 未包 `h()`/`esc()` 的位置），未发现把用户或 AI 文本直接拼进 HTML 的情况；未转义处均为数字或已转义片段。
6. **预览有独立 schema 校验**，且未知 profile 会报 `WB-PROFILE-001` 而不是静默返回空。

---

## 4. 本轮未覆盖 / 未验证

### 4.0 「方向层」覆盖地图（前三轮都在同一条链路里找 bug，以下是整块没看的方向）

#### 本轮新验证到的方向级问题

##### K1 运行时检索质量：keywords 只有内部 id + 标题，别名不进索引（高）

- `RuntimePackageCompiler.BuildEntry`：`keywords = [documentId] + 标题各语言值`（`RuntimePackageCompiler.cs:101-102`）；该文件全文 **`aliases` 出现 0 次**。
- 实测（打包版 CLI，`pravend-cluster` 的 geography 文档，其正文声明 `aliases.zh-CN: [巴拉维诺斯]`）：

```text
id=awake:entry:geography.pravend
  keywords   = doc.geography.pravend | 帕拉汶德
  entityRefs = awake:settlement:town_v3
```

- 三个后果：① **内部文档 id 被当成关键词**写进索引（玩家/NPC 永远不会这样提问）；② **别名"巴拉维诺斯"查不到该条目**——而官方 companion 口述仍在使用这个旧称；③ `entityRefs` 只进 `extensions`，不进 `keywords`/`index`，运行时若按地点检索同样拿不到。
- 与前面批次的关系：R8/R9 的"别名匹配"只做在 worker 侧锚点识别上，**运行包侧没有跟进**。这一项直接对着"NPC 调取越准确"的产品目标。
- 等级依据：`[代码]` + `[实测]`（上面输出为真实编译产物）。

##### K2 首次使用的硬前置依赖没有任何引导（高，FTUE）

- 生成面板的默认 Provider 是 `local`（`studio-draft.js` 的 `draftProvider` 首个选项即"本机 AI Worker"）。
- 发行包内**不含** worker 组件（包目录：`cli` / `contracts` / `schemas` / `web` + Launcher + 文档）。
- 在 `wwwroot`、`新手指引_世界书内容编辑者.md`、`README_使用说明.txt` 内检索 `Ollama`、`安装 Worker`、`启动 Worker`、`下载` 命中数为 0；新手引导只有一句限定语："本机 Worker 必须已经运行并完成配置"。
- 后果：一台干净的客户机器上，第一次点"开始生成草稿"必然失败，且界面/文档都没有可行动的下一步。
- 等级依据：`[代码]`（跨文件检索）。

##### K3 `/api/documents` 每次全量解析并校验整个工作区（中）

`ListDocuments()` → `_workspace.LoadAuthoringDocuments()`，每份文档都带 `Report.Valid` 与诊断（`Application.cs:42-57`）。首屏 `Promise.all` 的 4 个请求里有 **2 个是 O(工作区)**：本项，以及 U1 的 `editor-catalog`（604 KB 实体目录校验）。
等级依据：`[代码]`；只在 4 份文档的临时工作区上测过 133 ms，未做规模化测量。

##### K4 本地 API 的信任边界没有显式结论（需产品决策）

`WebAiSessionStore.RequireExactOrigin` 只要求请求头 `Origin: http://127.0.0.1:<port>`（`WebAiSessionStore.cs:132-135`）。**本机任意进程伪造该头即可 bootstrap 会话**，随后可读全部世界书、驱动 AI 生成、并走完 `register → approve → compile-proof → compile → export-staging` 权威链（现有 smoke 脚本正是这么做的）。
对"离线本地工具"这可能是有意取舍，但它是**权威门**的边界，应当显式记录并说明接受理由，而不是隐含在实现里。
等级依据：`[代码]`。

##### K5 没有工作区升级/迁移路径（中）

Core 内检索 `workspace_version` / `Migrate` / 迁移逻辑 **无命中**；草稿状态、批次、权威记录一律走 fail-closed / quarantine（如 `AuthoringDraftStore.QuarantineState`）。也就是说换包或契约升级后，旧工作区的草稿、批次、权威记录要么继续可用、要么整块作废，没有中间态策略。
等级依据：`[代码]`。

##### V1 （正面）AI 凭据保护

云端 API Key 用 **Windows DPAPI** 加密后存 `LocalAppData`（`ProviderSettingsStore` + `WindowsDpapiSecretProtector`），不写入工作区、世界书或普通日志。这一点是对的。

#### 还没看的整块方向（建议下一轮或另开会话）

> 状态（2026-09-10 第四轮结束时）：1–7 已按方向审完，结论见第 5 节；仅第 8 项仍未审。

1. ~~**跨文档内容一致性**~~ → 已审，见 **5.1**（D1-1/D1-2/D1-3）。
2. ~~**文风维度**~~ → 已审，见 **5.2**。
3. ~~**多实例与并发写入**~~ → 已审，见 **5.3**。
4. ~~**人工编辑后的内容漂移度量**~~ → 已审，见 **5.4**（D4-1/D4-2/D4-3）。
5. ~~**支持性与可观察性**~~ → 已审，见 **5.5**。
6. ~~**交付包卫生**~~ → 已审，见 **5.6**。
7. ~~**浏览器兼容与对比度**~~ → 已审，见 **5.7**（兼容风险已定性，**对比度仍未实测**）。
8. **迁移候选链路**：属另一会话范围，仍未审。

### 4.1 条目级未验证清单

- 未运行真实云端 Provider；G5 的"截断"结论是预算算术推出，未实测某具体模型的行为。
- B1/B5 的观感结论来自代码路径推演，**未在实机上跑长批次（≥20 项）与大批量工作区复现**。
- 未审 Persona Workbench / UI Workstation 侧本身的实现质量（本会话只审 Worldbook Studio）。
- G4 未构造端到端复现（需构造"同引文覆盖两个侧面"的候选集）。
- U1/U2 的耗时是**本机实测**，换机器绝对值会变，量级结论不变。
- 未做无障碍（键盘焦点、对比度）专项；未做多语言（界面全中文，无 i18n 需求）。

## 5. 方向审（第四轮起，按"方向"逐条审并即时落盘）

> 与前三轮的区别：前三轮在第 1–4 节里按**链路**找 bug；本节按**方向**审，每条审完即写入。

### 5.1 跨文档内容一致性 + 运行时检索歧义

#### D1-1 没有任何文档级重复/冲突检测（高）

- 在 `ValidationServices.cs` 与 `Application.cs` 中检索 `duplicate`/`conflict`：唯一命中是保存的 CAS 冲突（`Application.cs:239` 的 `AdvancedSaveCheckResult("conflict", …)`）。
- 也就是说：**文档级**没有"两条词条写同一件事""两条词条互相矛盾"的任何检测；已有的一致性能力只在**候选级**（`AuthoringReviewProjection` 的 `duplicate_candidate` / `candidate_conflict`）。
- 后果：两条互相矛盾、或内容高度重复的词条可以同时以 `canon` 存在并一起编译进同一个运行包（编译只做结构、来源、分层闭包校验——见 `ContentGraphBuilder` 的节点/边检查），不会给作者任何提示。这是世界书作者最痛的点，目前完全没有兜底。
- 等级依据：`[代码]`（检索 + 编译校验路径核对）。

#### D1-2 关键词歧义（一个词 → 多条词条）既无消解也无诊断（中）

- 运行包索引的结构本身就是多值：`index.keywordToEntryIds`（`tools/worldbook-contract/v1/runtime.schema.json:117-121`）。
- 写入方 `RuntimePackageCompiler.AddIndex` 只做"追加 + 去重 entryId"（`RuntimePackageCompiler.cs:287-291`），**不检测同一关键词被多个条目占用**，也不产生任何诊断。
- 叠加上 K1（keywords 只有内部 id + 标题）：两个同题或近题的词条（例如都叫"帕拉汶德"）会共享关键词，运行时拿到多个 entryId 只能自行消歧。
- 等级依据：`[代码]`。

#### D1-3 运行契约层根本没有"别名"概念（中，K1 的补充证据）

`runtime.schema.json` 的 entry 必填字段是 `id / domain / title / summary / expressions / keywords`，`keywords` 要求 `minItems=1`、`uniqueItems=true`；额外信息只能进 `extensions`（`entry_extensions` 为 `additionalProperties: true`）。**契约里没有 aliases 字段**。
也就是说：authoring 文档有 `aliases`（schema 有、编辑器高级信息里可见、实体目录合并时也用），但**运行契约侧没有对应概念** → 想修 K1 要么把别名折进 `keywords`（最小改动），要么扩契约新增字段（要改契约版本与消费端）。这个取舍需要显式决定。
等级依据：`[代码]`。

### 5.2 文风维度：只有自由文本，没有受控词表，也没有归属声明

- Studio 里与文风有关的只有两处，都是**自由文本**：
  - 生成意图字段 `draftStyleConstraints`（"文风与格式约束，一行一个"，`studio-draft.js:42` 的快速面板与 `:66` 的死代码副本各一份）；
  - 旧 AI 助手的分析维度 `prose`（"表达与文风"）与 `world_style`（"世界观文风"）（`studio-ai.js`）。
- authoring schema 中检索 `style` / `tone` / `voice` / `register` **无命中**：文档模型里没有文风字段。
- 而项目根 `AGENTS.md` 的"世界书 / rules 规范"把**四档文风**（clean 编年史体 / dark 流言体 / extreme 自述体 / bloody 余响体）列为世界书规范的一部分。

这不是实现缺陷，而是**范围与归属没有写明**的风险：本工具的 `content_tier`（base / adult_optional）与四档文风是两个完全不同的维度，界面上却都叫"内容范围 / 层级"这类词；使用者（包括我，见 U10 那条把 era 预设写错的同类问题）很容易把它们混为一谈。
**建议**：在 v3 手册与工具内明确一句"四档文风不属于本工具范围，本工具只产出 `awake_current` 宇宙的知识条目与 base/adult_optional 分层"。
等级依据：`[代码]`（检索 + schema 核对）。

### 5.3 多实例与并发：有工作区级保护，但缺少启动期的占用提示

先说**已经做对的**：

| 保护 | 位置 | 语义 |
|---|---|---|
| 草稿状态跨进程互斥 + 修订号 CAS | `AuthoringDraftStore.PersistStateUnsafe`（`Local\AWAKE.WorldbookStudio.DraftState.<statePath 哈希>`，30 s 等待，修订不符报 `WB-AI-DRAFT-CAS-409`） | 两个实例不会互相覆盖草稿状态 |
| 批次写租约 | `BatchWorkspaceWriteLease`（`WB-BATCH-WRITE-409: 该批次正在由另一个工作室实例写入。`） | 单批次串行写 |
| 编译工作区租约 + 目标租约 | `AuthorityGate` 的 `.compile-workspace-lease` / per-target lease（`FileShare.None`） | 编译串行化 |
| 权威记录严格/遗留双路径校验 | `AuthorityGate.ResolveStorageRecordPath` | 两套记录不一致时报 `WB-AUTHORITY-SAFEID-409` |

**缺的那一环**：互斥体按**包路径**命名 —— `StudioRuntimeHashing.PackageMutexName(packageRoot)`（`LauncherForm.cs:123`），含义是"同一份解压目录只允许开一个窗口"。
于是：
- 用户如果把新版解压到**另一个目录**（很常见：`WorldbookStudio-v2\` 与 `WorldbookStudio-v3\` 并存），两个实例可以同时打开**同一个工作区**；
- 此时没有任何**启动期**的占用检测（检索 `workspace_version`/`WorkspaceLease`/`.lock` 均无工作区级单例），冲突只会在干活干到一半时以 `Draft 状态文件正在由另一个工作室实例写入。`、`WB-BATCH-WRITE-409` 这类**中途中止**的形式冒出来。

**影响**：保护是有的（不会静默覆盖），但用户体感是"随机报错"，且不知道是"另一个窗口占用了我"。
**建议**：在工作区根加一个可读的占用标记（pid/实例号），启动与关键写入前检查并给出统一提示"该工作区正被另一个工作室窗口使用"。
等级依据：`[代码]`。

### 5.4 人工编辑后的漂移：拦得住"加料"，拦不住"删字变义"，且改动没有留档

先把**做对的**写清楚（这三层是有效的）：

1. **编辑只能落在原引文范围内**：`AuthoringDraftEditValidator.EnsureEditedTextIsSourceBound` 调 `SourceEvidenceMatcher.TryFind(item.Quote, editedText)`——参数序是"引文=haystack、编辑后文本=needle"，语义为**编辑后的文本必须仍是原引文的子串**（`SourceEvidenceMatcher.cs:24-46` 用 `source.IndexOf(candidate)` 实现，并做全角/标点/空白规范化）。作者可以删减、不能加料。
2. **编辑即降级证据**：文本改动后 `ReviewStatus=author_modified`、`Evidence` 被 `MarkUnverified`（`Verified=false`）。
3. **编辑后重新复查 must_not_invent**：命中禁止词直接阻断建档（`WB-AI-DRAFT-EDIT-422`）。

对照 R1 的调用序 `TryFind(fileContent, quote)`（`ValidationServices.cs:137`）可以确认两处参数序都是"容器在前、被查文本在后"，没有写反。

#### D4-1 子串包含拦不住极性/否定漂移（中）

因为校验只要求"编辑后文本是引文的子串"，**从引文里删掉否定词仍然合法**：
引文"……一直没有被攻破"，作者改成"……一直被攻破"——仍是原引文的子串，`EnsureEditedTextIsSourceBound` 通过。
唯一的缓冲是第 2 条的"证据降级 → 候选风险标红（`unverified_evidence`）"，属**间接提示**，不是阻断。
等级依据：`[代码]`。

#### D4-2 建档后"证据已降级"这条信息反而丢了（中）

`AuthoringDraftDocumentBuilder.AuthorCreated` 写入文档的证据对象只有 `reference_id / locator / quote / quote_hash`（必要时 `locator_object`），**不写 `verified`/`evidence_verified`**（`AuthoringDraftDocumentBuilder.cs:170-181`）。
后果：进入文档层后，"这条内容经过作者改写、原文依据已不再可信"这一状态**只剩下** `author_created.review_status` 与 `fact_annotations[].review_status` 这类粗粒度标记；下游（工作区校验、编译）只看引文能否在来源文件里定位，而改写后的引文通常仍能定位，于是**改写痕迹在语义层被抹平**。
等级依据：`[代码]`。

#### D4-3 没有"AI 原文 ↔ 作者改后"的对照留存，也没有漂移度量（中）

检索 `original_text` / `aiText` / `original` 在文档构建与编辑器模型侧：只用于"读原文—合并—覆盖"这一路径，**没有保存生成原文**。
文档里能反映"被作者改过"的只有：`author_modified` 标记、以及 metadata 侧的 `modified_fields`（`AuthoringDraftEditValidator.RevalidateMetadata`）。
后果：事后无法回答"这条 canon 有多少来自 AI、作者改了什么"，也无法批量统计"AI 产出被改写的比例"——而这恰恰是评估生成质量最直接的指标（也直接影响我们后续调 prompt 的效果度量）。
等级依据：`[代码]`。

### 5.5 支持性与可观察性：出问题时用户手里什么都没有

| 事实 | 证据 |
|---|---|
| 服务端**有**构建号：`/api/health` 返回 `build_id`（程序集版本） | `Program.cs:84` |
| 但 **UI 不显示版本**：前端对 `buildId`/`build_id` 的引用为 0 | `wwwroot` 全目录检索 |
| 日志目录是 `%LocalAppData%\AWAKE\WorldbookStudio\logs\`（`launcher-YYYYMMDD.log` / `web-YYYYMMDD.log`），**界面不告知** | `LauncherLog.cs:10-18`、`WebRuntimeBootstrap.cs:98` |
| UI 里唯一出现"日志"字样的地方，是那句"API Key 不会写入世界书、工作区、**日志**或导出包" | `index.html:49` |
| `correlation_id` 服务端**有**（批量错误体 `BatchEndpoints.Failure`、交接错误体），前端**0 处引用** → 用户只能看到 `（WB-BATCH-XXX）`，无法与日志/工单对应 | `wwwroot` 全目录检索 `correlation` = 0 |
| 运行期日志覆盖极窄：只有权威链错误会落 `LogError`（`AuthorityHttpErrorProjection.cs:69-70`）；普通请求失败、AI 生成失败、校验失败**不写日志** | `src/Awake.WorldbookStudio.Web` 内检索 `ILogger|LogError|AppendAllText` |
| 没有任何"诊断导出"入口：路由清单里没有 doctor/diagnostics/export-logs 类端点（CLI 有 `doctor`，UI 无入口） | `Program.cs` 路由清单 |

**影响**：用户遇到问题时只能截图一个错误码；支持方拿不到版本号、日志与请求 ID，排障成本高。对"交付给顾客"的产品这是明确的可支持性缺口。
**建议**：UI 页脚显示 `build_id` + 日志目录路径（可一键打开）；错误提示里带上 `correlation_id`；补一个"导出诊断包"（脱敏后的日志 + 版本 + 工作区结构摘要，不含正文）。
等级依据：`[代码]`。

### 5.6 交付包卫生：重复载荷 + 一处写死的开发机路径

以最新一份客户交付包 `artifacts/customer-delivery/awake-customer-20260906-130127495-…` 为样本：

- 交付总量 **574.6 MB / 667 个文件**（`Get-ChildItem -Recurse | Measure-Object`）。
- 其中 **175 MB 是"解压树 + 同内容 zip"的双份载荷**：

```text
worldbook-studio/  解压树（Launcher.exe 115 MB + cli/web/schemas…）
worldbook-studio/worldbook_studio.zip          130.9 MB
persona-workbench/persona_workbench.zip         44.2 MB
ui-workstation/ui_workstation.zip                5.7 KB
```

  · 风险不在体积本身，而在于**同一份内容有两份权威**（解压树与 zip 各自带 `.sha256`/`SHA256SUMS.txt`），校验时容易只核一份；交付说明里没有写"到底以哪份为准、为什么两份都要带"。
- **写死的开发机路径**：`ui-workstation/UiWorkstation.Adapter.ps1:21`

```powershell
$gameRoot = [System.IO.Path]::GetFullPath('D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord')
```

  它被用于"**不得把运行时文件写进游戏目录**"的安全检查（`:21-27` 与 `$runtimeFull.StartsWith($gamePrefix)` 比较），且脚本没有 `-GameRoot` 参数。
  后果：在客户机上（游戏装在别处）这条检查**永远不会命中** —— 不是"路径填错导致功能坏"，而是**安全检查退化为假阴性**，看起来有保护、实际没保护。
- **没夹带**本机绝对路径、`_tmp`、`awake-pravend-*` 之类的测试痕迹（除上面那一处脚本）；`manifest.json` 只列包内相对路径 —— 这点是干净的。
- `ui-workstation` 本身是 PowerShell 适配器（`start/stop-ui-workstation.ps1` + `Adapter.ps1` + 5.7 KB zip，没有应用目录），属设计如此；但交付说明需要写明它依赖 PowerShell 与用户自己的游戏安装位置。

等级依据：`[实测]`（对交付目录做体积统计与内容检索）。

### 5.7 浏览器兼容与对比度

- 启动器用**系统默认浏览器**打开界面：`Process.Start(new ProcessStartInfo(address) { UseShellExecute = true })`（`LauncherForm.cs:13-17`，经 `ShellBrowserOpener`），**不做任何浏览器能力检测**。
- 前端依赖 `<dialog>.showModal()`（5 处，`studio-draft.js` 2 处 + `studio-batch.js`/`index.html` 等）；未检索到 `structuredClone`、`Object.hasOwn`、`replaceAll`、`Intl.Segmenter` 等更高风险的 API（命中 0），因此兼容风险集中在 dialog 与整体现代语法。
- 失败形态：若默认浏览器过旧（IE11 / 旧版 Edge），表现为"页面能开但按钮没反应/对话框不出现"，且没有任何友好提示（`WB-BROWSER-OPEN-FAILED` 只覆盖"打不开浏览器"这一种情况）。
- 对比度：本次无渲染环境，**未实测**；只能确认 CSS 含 `:focus-visible` 焦点样式。
等级依据：`[代码]`；对比度项标为未验证。

---

## 6. 无副作用声明

未修改任何代码或配置；未启动游戏；未读写或同步游戏目录；未访问云端 Provider / 真实 API Key；
期间只以 dev 模式启动过打包版 Web（仅 127.0.0.1 回环）做只读计时，并在测量后立即结束进程。
