# Plan: Worldbook Studio 五大知识分类与二级主题
_Locked via grill — revised after independent read-only review round 2_

## Goal
修正 Worldbook Studio 当前只有政治、经济、文化、战争四项的问题，建立面向普通内容编辑者的五大主分类：政治、经济、文化、战争、地理；为每个主分类提供直白的二级主题，并让地理档案能够完成当前 Studio 的 authoring→v2 runtime 编译链。此次批次只扩展世界知识域合同和 Studio 分类元数据：不改变 NPC 权限算法，不扩展周报/事件的四类业务分类，不修改游戏目录、冻结候选或旧四版运行时内容。

## Confirmed boundary
- `tools/worldbook-contract/v1` 虽然目录名是 `v1`，其中的 `runtime.schema.json`、`common.schema.json` 和 `enums.json` 实际是当前 Studio F22 使用的 `awake.worldbook.runtime.v2` 合同；本批次只把该知识域枚举从四项扩展为五项。
- `WeeklyReportService`、`WorldEventContracts`、事件/周报 Schema 的四类仍表示事件分栏，不等于世界知识域，不在本批次扩展。共享 `common.schema.json` 不直接放宽：新增 `knowledge_domain`（五类）与 `event_domain`（四类），运行时/知识条目引用前者，事件记录/周报引用后者。
- `awake.worldbook.authoring.v1` 是 Studio 作者文件格式；它可增加 `geography`、`subdomain`、`related_domains`，旧作者文件仍按兼容规则读取。
- `awake.worldbook.v2` 运行时继续按主 `domain` 调取；二级主题和相关分类先作为作者组织、检索索引和 AI 建议元数据，不变成新的权限条件。

## Approach
1. 新增唯一分类目录：
   - 源文件：`_houkai_merge/AWAKE/docs/worldbook-studio-plan/knowledge-taxonomy.v1.json`。
   - 配套 Schema：`knowledge-taxonomy.v1.schema.json`。
   - 发布包路径：`schemas/knowledge-taxonomy.v1.json` 与 `schemas/knowledge-taxonomy.v1.schema.json`，沿用现有 package 脚本复制 `docs/worldbook-studio-plan` 的机制。
   - 文件包含 `schema_version`、`taxonomy_version`、按固定顺序排列的五个 domain、中文 label/help、二级主题稳定 ID、主题说明、例子和冲突归类提示。启动/目录读取时校验版本、五类唯一性、重复二级 ID 和主题归属。
   - `taxonomy_hash` 定义为分类 JSON 文件原始 UTF-8 字节的 SHA-256；不对换行或键顺序做二次规范化，文件顺序就是 UI 顺序。配套 Schema 不混入该 hash，另由 package manifest 登记。
2. 定义机器契约：
   - 文档 `subdomain` 为可省略的小写稳定 ID，正则 `^[a-z][a-z0-9_]*$`；省略表示旧档案未补充，禁止 `null` 和空字符串。
   - 文档 `related_domains` 为可省略数组；省略表示无相关分类，出现时最多 4 项、唯一、保持 taxonomy 文件顺序、不允许未知 ID、不允许重复、不允许等于主 `domain`，禁止 `null`。
   - UI/HTTP 模型使用 `subdomain`、`relatedDomains`；authoring YAML/JSON 使用 `subdomain`、`related_domains`；服务端只接受 taxonomy 当前版本中的稳定 ID，不做大小写或别名自动纠正。
   - 保存快照和编辑器 API 的精确字段为：`/api/editor-catalog` 顶层 `taxonomyVersion`/`taxonomyHash`，`/api/editor-document` 顶层同名字段，`SaveEditorDocumentRequest` 的 JSON 字段同名；保存比较请求值与当前 raw-byte hash，目录变化返回 `WB-TAXONOMY-CAS-409`、当前版本/哈希和重新读取提示。taxonomy 文件或配套 Schema 缺失、解析失败或版本不支持时 fail closed，返回 `WB-TAXONOMY-500`，不使用四类 fallback。
3. 更新 Studio authoring Schema、Core 和模板：
   - `domain` 与文档 ID 接受五类；`subdomain`、`related_domains` 写入 authoring Schema 并保留 `additionalProperties:false` 的结构约束。
   - Core 使用 taxonomy 目录做主分类/二级主题/相关分类校验，不再在 `Application.cs` 或 `AuthoringEditorModel.cs` 各自写一份 domain literal。
   - 新建 API 和 `CreateGeneratedDocument` 要求有效二级主题；保留旧方法调用兼容，未传二级主题的历史测试/旧档案仍可读写但会显示补充提示。
   - 模板仅在字段有值时写入可选字段；空数组不写入，避免把“没有相关分类”和“旧文件未记录”混成同一种机器状态。
4. 接通编辑器和 AI 投影：
   - `/api/editor-catalog` 返回五类、taxonomy 版本/哈希、中文说明、例子和每类二级主题；主要界面不展示英文 ID。
   - 新建对话增加“二级主题”和“相关分类”；主分类改变时清理不匹配的二级主题及相关分类，并显示归类提示。
   - 作者模式基本信息显示并保存二级主题/相关分类；旧档案缺失时显示可点击的中文警告而不是阻止读取。
   - `AssistanceService` 的既有 document projection 增加两个字段，并同步更新内部 `assistance.request.v1` Schema；不另造 Provider 协议，不把 taxonomy 文件正文发送给云端。
5. 扩展当前知识域运行时合同：
   - 拆分 `tools/worldbook-contract/v1/common.schema.json` 的 `knowledge_domain` 与 `event_domain`：知识条目和当前 v2 runtime 引用五类 `knowledge_domain`，事件记录和周报继续引用四类 `event_domain`；`enums.json` 同时明确 `knowledgeDomains` 五项与 `eventDomains` 四项。补充合同文档说明该目录承载当前 v2 runtime contract。
   - `RuntimePackageCompiler` 输出主 domain，并把元数据固定在每个 `entries[*].extensions`：可选 `sourceDocumentId`、`subdomain`、`relatedDomains`；运行时 Schema 对这些字段使用 `additionalProperties:false`、稳定 ID 正则、最多 4 项、唯一性和 taxonomy 顺序约束，运行时读取器可忽略它们，主 domain 行为不变。
   - 不修改 `WeeklyReportService`、`WorldEventContracts`、事件 Schema、周报四分类或 NPC 权限决策代码。
6. 增加可机检测试与文档：
   - `TAX-001` taxonomy Schema、五类顺序、中文标签、版本/hash。
   - `TAX-002` 新建 `doc.geography.*` 必须带有效二级主题。
   - `TAX-003` 二级主题错配、未知主题、未知/重复/自引用相关分类、空值/缺失值边界。
   - `TAX-004` 四 domain × YAML/JSON × source/author-created 的最小兼容矩阵，保存后保留 sources、author_created、authority、registry binding、revision 和未修改字段。
   - `TAX-005` JSON/YAML round-trip、编辑合并和 taxonomy CAS。
   - `TAX-006` 地理档案创建→读取→作者编辑→预览→编译，验证 runtime.json、index.json、package-manifest.json、content/package hash 和五类 domain index。
   - `TAX-007` AI document projection 保留两个新字段，旧 Provider response/golden 字段不变。
   - `TAX-008` 使用版本化白名单 `tests/fixtures/studio-taxonomy-domain-allowlist.v1.json` 扫描固定根目录 `src/Awake.WorldbookStudio.Core`、`src/Awake.WorldbookStudio.Web/wwwroot`、Studio tests 和 Studio docs；仅允许 `politics|economy|culture|war` 出现在该 fixture 列出的旧兼容测试/文档位置，Studio catalog、模板、Core 校验和新建 UI 的允许计数必须为 0。旧 runtime/事件合同不纳入扫描根，另由合同测试保护。
   - 更新新手指引、分类归类表和错误提示；执行 Studio harness、前端语法检查、Release build、package/release-check 与限定范围代码债务审查。

## Taxonomy

### 政治 `politics`
王位、王国、领地、官职、法律、外交、家族、继承。

### 经济 `economy`
土地、粮食、贸易、税赋、货币、工坊、债务、商路。

### 文化 `culture`
信仰、习俗、语言、身份、婚姻、服饰、节庆、艺术。

### 战争 `war`
战史、军制、兵种、武器、战术、要塞、军需、俘虏。

### 地理 `geography`
地形、气候、方位、河流、道路、聚落、边界、资源、航路。

## Key decisions & tradeoffs
- 主分类是唯一必选归属，避免一条知识同时出现在多个主目录造成重复调取。
- 二级主题采用直白中文名称，机器使用稳定小写 ID；普通编辑者只在下拉框中看到中文名称和说明。
- 归类冲突使用目录中的固定提示：物理边界归地理、主权边界归政治；聚落位置归地理、聚落行政归政治；土地生产归经济、土地地貌归地理；资源分布归地理、资源交易归经济；要塞防御归战争、要塞位置归地理；商路经营归经济、道路走向归地理。
- `related_domains` 只记录相关主分类，不改变主分类，也不自动复制知识内容。
- 新建流程必须选择二级主题；历史档案缺少 `subdomain` 时不强制迁移，以保证旧 Studio authoring v1 文档兼容，并在作者界面给出补充提示。
- 运行时 v2 只按主分类读取；二级主题和相关分类通过 `extensions` 传递并可被未来索引使用，不参与当前权限算法。

## Acceptance contract
- `/api/editor-catalog` 返回五个主分类、固定版本和 raw-byte hash；每个主分类返回中文二级主题、说明、例子和冲突提示。
- 新建档案可以创建 `doc.geography.*`，必须选择有效二级主题，生成的文档通过 Studio authoring v1 Schema。
- 旧 `doc.politics.*`、`doc.economy.*`、`doc.culture.*`、`doc.war.*` 文档在 YAML/JSON、source/author-created 四组合中仍可读取、预览和保存，且未修改字段不丢失。
- 二级主题与主分类不匹配时保存失败；未知、重复、空字符串、null 或包含主分类的相关分类不能保存。
- 跨领域档案只保留一个 `domain`，相关分类显示为辅助标签，并按 taxonomy 文件顺序稳定保存。
- 普通界面不显示 `politics`、`geography` 等内部 ID 作为主要编辑文本；高级模式继续保留机器字段。
- JSON/YAML round-trip 保留两个新字段；旧文档缺少字段不会被强行写入空值。
- taxonomy 文件变化后，旧编辑器保存请求返回 `WB-TAXONOMY-CAS-409`，响应包含当前 `taxonomyVersion`/`taxonomyHash`；刷新后才能保存。taxonomy 缺失或不支持时返回 `WB-TAXONOMY-500`，不降级到四类。
- 地理档案完成创建→读取→作者编辑→预览→v2 编译；输出的 runtime/index/manifest 通过当前 v2 合同，五类均有 domain index，包 hash 可重算。
- Studio 自身不再残留四类下拉或校验 fallback；旧 runtime/事件合同中的四类定义保持不变并有静态白名单。

## Risks / open questions
- 当前 v2 runtime 读取器会忽略 `extensions` 中的二级主题；若未来要按二级主题检索或授权，另开运行时算法与性能审查。
- `tools/worldbook-contract/v1` 的目录命名与 v2 schema id 不一致；本批次不重命名目录，只拆分 knowledge/event domain 引用并最小扩展知识枚举。
- 旧 AI provider golden 只保护 suggestion envelope 字段；document projection 增加两个可选字段，需通过内部 request schema 和 round-trip 测试确认。
- 真实 Bannerlord、游戏目录、PlayerExports 和冻结候选仍不在本批次范围。

## Out of scope
- 不实现人物/家族运行时权限结算。
- 不实现知识引用图、自动拆分多领域档案或自动复制条目。
- 不改变 `awake.worldbook.v2` 运行时读取器的权限算法。
- 不扩展周报、事件、WorldEventContracts 或 WeeklyReportService 的四类业务分类；它们继续引用四类 `event_domain`。
- 不启动游戏，不同步游戏目录，不修改 `PlayerExports`、dist 或冻结候选。


## Implementation contract amendments

### Exact taxonomy IDs and order
`knowledge-taxonomy.v1.json` 的 `domains` 固定按以下顺序出现；每个 `subdomains` 也按列出顺序保存和显示。每项包含 `id`、`label.zh-CN`、`help.zh-CN`、`examples.zh-CN`。

- `politics` 政治：`throne` 王位与王权、`kingdoms` 王国与势力、`territories` 领地与统治、`offices` 官职与制度、`law` 法律与裁判、`diplomacy` 外交与关系、`clans` 家族与血缘、`succession` 继承与婚盟。
- `economy` 经济：`land_production` 土地与生产、`food` 粮食、`trade` 贸易、`taxation` 税赋、`currency` 货币、`workshops` 工坊、`debt` 债务、`trade_routes` 商路。
- `culture` 文化：`faith` 信仰、`customs` 习俗、`language` 语言、`identity` 身份、`marriage` 婚姻、`clothing` 服饰、`festivals` 节庆、`arts` 艺术。
- `war` 战争：`war_history` 战史、`military_system` 军制、`troops` 兵种、`weapons` 武器、`tactics` 战术、`fortifications` 要塞与防御、`logistics` 军需、`prisoners` 俘虏。
- `geography` 地理：`terrain` 地形、`climate` 气候、`directions` 方位、`rivers` 河流、`roads` 道路、`settlements` 聚落、`natural_boundaries` 自然边界、`resources` 资源分布、`sea_routes` 航路。

归类判定固定为：物理边界→`geography.natural_boundaries`，主权边界→`politics.territories`；聚落位置→`geography.settlements`，聚落行政→`politics.territories`；土地生产→`economy.land_production`，土地地貌→`geography.terrain`；资源分布→`geography.resources`，资源交易→`economy.trade`；要塞防御→`war.fortifications`，要塞位置→`geography.settlements`；商路经营→`economy.trade_routes`，道路走向→`geography.roads`。目录中的 `help` 和 `examples` 对作者显示这些反例。

### Schema reference matrix
- `common.schema.json` 删除旧的单一 `domain` 作为业务引用，新增 `$defs.knowledge_domain`（五类）和 `$defs.event_domain`（四类）；保留旧 `$defs.domain` 仅作为禁止新引用的兼容别名，值仍为四类，避免未知外部引用被意外放宽。
- `runtime.schema.json` 的 `knowledge_entry.domain` 精确引用 `common.schema.json#/$defs/knowledge_domain`；`event-record.schema.json:properties.domain` 与 `weekly-report.schema.json:...section.properties.domain` 精确引用 `...#/$defs/event_domain`。合同测试必须证明知识条目接受 `geography`、事件记录和周报拒绝 `geography`。
- `enums.json` 同时写入 `knowledgeDomains` 五项和 `eventDomains` 四项；原 `domains` 字段不再作为新代码来源，若保留则固定为四类兼容字段并加说明。

### Runtime entry extensions
仅 `runtime.schema.json` 的 `knowledge_entry.properties.extensions` 使用专用 `$defs.entry_extensions`，不改变根对象 `extensions` 和 expression `extensions` 的现有开放兼容行为。`entry_extensions` 为可选 object，禁止 `null`，`additionalProperties:false`，字段如下：
- `sourceDocumentId`：可省略，非空字符串，格式为 `^doc\\.(politics|economy|culture|war|geography)\\.[a-z0-9]+(?:[._-][a-z0-9]+)*$`。
- `subdomain`：可省略，非空字符串，格式为 `^[a-z][a-z0-9_]*$`。
- `relatedDomains`：可省略数组，最多 4 项，元素为五类 `knowledge_domain`，唯一，不允许 `null`。
- `subdomain` 必须属于同一 entry 的 `domain`；`relatedDomains` 不得包含 entry 的主 `domain`，且必须按 taxonomy 文件 domain 顺序排列。这两项是 Core 编译校验（错误码 `WB-TAXONOMY-422`），Schema 负责类型/正则/唯一性/数量边界；旧 runtime 中没有 extensions 或只有 `sourceDocumentId` 仍合法。
- `RuntimePackageCompiler` 只在 authoring 字段存在时写 `subdomain`/`relatedDomains`，不写空字符串或空数组；保留根级 `extensions` 的 `contentTier`/`source` 和 expression 级 `sourceAssertionId`。

### Taxonomy source, copy and hash
- 开发/测试权威源只有 `docs/worldbook-studio-plan/knowledge-taxonomy.v1.json`；Studio 运行时从传入的 `SchemaRoot` 读取同名文件，源码环境的 SchemaRoot 指向上述目录，发布环境的 SchemaRoot 指向包内 `schemas`。
- `package.ps1` 复制后立即用原始字节 SHA-256 比较源文件与 `artifacts/WorldbookStudio/schemas/knowledge-taxonomy.v1.json`；`manifest.json` 登记 `taxonomyVersion`、小写 64 位 `taxonomyHash` 和配套 Schema 的 `taxonomySchemaHash`。`release-check.ps1` 重新计算并拒绝不一致。Studio 不同时读取两份。
- 支持的唯一 `taxonomy_version` 是 `1.0.0`；版本/哈希均大小写固定（版本原样、hash 小写）。taxonomy JSON 或 Schema 缺失、UTF-8/JSON 解析失败、Schema 不合法、版本不支持、五类重复或二级 ID 重复时 fail closed，错误码 `WB-TAXONOMY-500`，所有入口不写工作区。

### CAS wire contract
- `GET /api/editor-catalog` 成功返回顶层 `taxonomyVersion`、`taxonomyHash`、`domains`。
- `GET /api/editor-document` 成功返回顶层 `taxonomyVersion`、`taxonomyHash`，与 `registry`、`model` 同级。
- `POST /api/save-editor-document` 请求必须同时带 `taxonomyVersion`、`taxonomyHash`；缺失/格式错误返回 HTTP 400：`{ok:false,error:"WB-TAXONOMY-CAS-400",message:"分类目录快照无效，请重新读取档案。"}`，不写入。
- 版本和 hash 必须同时匹配当前目录；不匹配返回 HTTP 409：`{ok:false,error:"WB-TAXONOMY-CAS-409",message:"分类目录已更新，请刷新后再保存。",taxonomyVersion:<current>,taxonomyHash:<current>}`，不写入。
- taxonomy 不可用返回 HTTP 500：`{ok:false,error:"WB-TAXONOMY-500",message:"分类目录不可用，工作室已停止本次操作。"}`，不写入。成功响应的 `editorDocument` 必须继续带当前两个字段。

### New-document and AI wire details
- `/api/document/new` 的 JSON 请求为 `title`、`domain`、`subdomain`、可选 `relatedDomains`、`contentTier`、可选 `path`/`documentId`/`authorId`；自动生成和显式路径两条分支都把二级主题传给 `CreateGeneratedDocument`/`CreateDocument`。普通前端发送 `title/domain/subdomain/relatedDomains/contentTier`，不发送内部 ID。
- 旧 `CreateDocument` 调用保留兼容重载，省略二级主题只允许历史测试/导入路径；新建 Web API 始终要求二级主题。
- AI document projection 使用 authoring 原字段名 `subdomain`、`related_domains`，两个字段缺失时省略，禁止 null/空数组；`assistance.request.v1.schema.json` 仅新增这两个可选属性，AI suggestion envelope、Provider HTTP wire 和旧 golden 字段不变。TAX-007 fixture 固定新旧文档各一例。

### Static allowlist contract
`tests/fixtures/studio-taxonomy-domain-allowlist.v1.json` 的 Schema 固定为 `schema_version`、`scan_root`、`rules[]`；每条规则包含 repo-relative 精确 `path`、`literal`（四类正则之一）、`max_occurrences` 和 `reason`。扫描器只扫描 `src/Awake.WorldbookStudio.Core/**/*.cs`、`src/Awake.WorldbookStudio.Web/wwwroot/**/*.{html,js}`、`tests/Awake.WorldbookStudio.Tests/Program.cs` 和 `新手指引_世界书内容编辑者.md`；逐文件逐 literal 统计完整词边界，未列出的路径出现四类 literal 直接失败，列出的路径超过上限也失败。旧 runtime/事件合同不在扫描根，由 Schema/合同测试保护。

## Final executable clarifications

### Complete domain reference replacements
The implementation must make these exact replacements and add tests for each:
- `tools/worldbook-contract/v1/common.schema.json`: `$defs.knowledge_entry.properties.domain` changes from `$defs.domain` to `$defs.knowledge_domain`; `$defs.domain` is not used by new schemas.
- `tools/worldbook-contract/v1/authoring.schema.json`: every knowledge entry domain reference changes to `$defs.knowledge_domain`.
- `tools/worldbook-contract/v1/runtime.schema.json`: `$defs.knowledge_entry.properties.domain` references `common.schema.json#/$defs/knowledge_domain`; its local `$defs.domain` becomes `$defs.knowledge_domain` with five values.
- `tools/worldbook-contract/v1/event-record.schema.json:properties.domain` and `weekly-report.schema.json:section.properties.domain` reference `$defs.event_domain` with four values. No event/weekly file may reference `knowledge_domain`.
- `tools/worldbook-contract/v1/enums.json`: `knowledgeDomains` is five values and `eventDomains` is four values; any retained `domains` field is explicitly legacy event-only and remains four values.

### Entry extension compatibility and empty arrays
`entry_extensions` is the dedicated schema for `runtime.entries[*].extensions`: it is optional and non-null; it has constrained reserved properties `sourceDocumentId`, `subdomain`, and `relatedDomains`, while `additionalProperties:true` preserves unrelated legacy extension keys. Root-level and expression-level extension schemas remain unchanged. `relatedDomains` has `minItems:1` when present; an explicit empty array is rejected by authoring/Core and the compiler omits the property rather than serializing an empty array. Unknown legacy extension keys are not rewritten or removed.

### One taxonomy validator for every authoring path
`TaxonomyCatalogService` is the single Core validator used by authoring read diagnostics, save merge, both new-document branches, imported/raw save validation, workspace validate, compile, preview, AI consent, and CLI `validate/compile/preview/export/doctor`. It checks domain, subdomain ownership, related-domain membership/uniqueness/self-reference/order and taxonomy CAS. Missing subdomain is a warning only for legacy reads; new Web creation and explicit author-mode save of a new draft require it. No caller may add a second literal list.

### Static allowlist concrete fixture
`tests/fixtures/studio-taxonomy-domain-allowlist.v1.json` has `scan_root` equal to the repository-relative Studio root and these exact rules after the implementation:
- `src/Awake.WorldbookStudio.Core/**/*.cs`: each of `politics`, `economy`, `culture`, `war`, `max_occurrences: 0`.
- `src/Awake.WorldbookStudio.Web/wwwroot/**/*.{html,js}`: each of the four literals, `max_occurrences: 0`.
- `src/Awake.WorldbookStudio.Cli/**/*.cs`: each of the four literals, `max_occurrences: 0`.
- `src/Awake.WorldbookStudio.Launcher/**/*.cs`: each of the four literals, `max_occurrences: 0`.
- `tests/Awake.WorldbookStudio.Tests/Program.cs`: `politics:42`, `economy:0`, `culture:6`, `war:2`, with reason “legacy four-domain compatibility fixtures”; other paths and literals are forbidden.
- `新手指引_世界书内容编辑者.md`: each literal `max_occurrences:0`; Chinese labels are required instead.
The fixture schema requires `schema_version`, `scan_root`, `rules`; each rule requires `path`, `literal`, `max_occurrences`, `reason`. The scanner rejects paths outside these exact rules and uses whole-word matching.

### GET fail-closed wire behavior and entry matrix
`GET /api/editor-catalog` and `GET /api/editor-document` catch taxonomy failures and return HTTP 500 with exactly `{ok:false,error:"WB-TAXONOMY-500",message:"分类目录不可用，工作室已停止本次操作。"}`; no stack trace or path is exposed. The shared `EditorFailure` mapping explicitly maps `WB-TAXONOMY-500` to 500.
For missing/invalid taxonomy, all routes fail closed as follows: catalog/read/new/save-editor/validate/compile/preview/AI consent return no workspace write; Web uses 500 `WB-TAXONOMY-500` except missing CAS fields (`WB-TAXONOMY-CAS-400`, 400) and mismatched CAS (`WB-TAXONOMY-CAS-409`, 409). CLI commands return nonzero and the same error code in JSON; `doctor` reports the code without writing. `save-authoring` may write the raw requested file only when taxonomy is valid; otherwise it rejects before write. `compile`/`export` never write compiled or candidate output on taxonomy failure. Duplicate IDs, invalid order, invalid hash computation and unsupported version all use `WB-TAXONOMY-500`.

## Binding resolutions (supersede any earlier conflicting wording)

1. **Single Schema reference authority**
   - Studio authoring file: `docs/worldbook-studio-plan/awake.worldbook.authoring.v1.schema.json` changes its `id` pattern and `domain` enum to five values, and adds optional `subdomain`/`related_domains`.
   - Current v2 contract: `tools/worldbook-contract/v1/common.schema.json` renames `$defs.domain` to `$defs.knowledge_domain` (five values) and adds `$defs.event_domain` (four values). Its `$defs.knowledge_entry.properties.domain` references `awake.worldbook.contract.common.v1#/$defs/knowledge_domain`.
   - `tools/worldbook-contract/v1/authoring.schema.json` keeps its `entries` reference to `common.schema.json#/$defs/knowledge_entry`; it therefore receives five knowledge domains transitively and is explicitly covered by the contract test.
   - `tools/worldbook-contract/v1/runtime.schema.json` uses its one local `$defs.knowledge_domain` (five values) for `properties.knowledge_entry.properties.domain`; it does not reference a second domain definition and has no local `$defs.domain` alias.
   - `event-record.schema.json:properties.domain` uses exactly `common.schema.json#/$defs/event_domain`; `weekly-report.schema.json:properties.sections.items.properties.domain` uses exactly the same event reference. No event or weekly schema references `knowledge_domain`.
   - `enums.json` has `knowledgeDomains` (five values) and `eventDomains` (four values); no new code reads a generic `domains` field.

2. **Entry extensions compatibility**
   - `runtime.schema.json` uses `$defs.entry_extensions` only for `knowledge_entry.properties.extensions`. `entry_extensions` is optional, non-null, and has reserved typed fields `sourceDocumentId`, `subdomain`, `relatedDomains`, with `additionalProperties:true` to preserve unknown legacy extension keys. This supersedes any earlier `additionalProperties:false` wording for entry extensions; root and expression extensions remain unchanged.
   - `sourceDocumentId` is optional and matches `^doc\\.(politics|economy|culture|war|geography)\\.[a-z0-9]+(?:[._-][a-z0-9]+)*$`; `subdomain` is optional and matches `^[a-z][a-z0-9_]*$`; `relatedDomains` is optional with `minItems:1`, `maxItems:4`, unique five-class items. `null` and explicit empty arrays are rejected by Core; compiler omits absent values. Unknown legacy keys round-trip unchanged.
   - Core checks subdomain ownership, related-domain membership/order/self-reference; Schema checks type/regex/cardinality/uniqueness. This separation is intentional and tested.

3. **Exact authoring path reuse**
   - `TaxonomyCatalogService` is called by `Workspace.ReadAuthoring` diagnostics, `SaveAuthoring` validation, `SaveEditorDocument` merge, generated and explicit `CreateDocument`, raw import validation, `Validate`, `Compile`, `Preview`, AI consent creation, and CLI `validate/compile/preview/export/doctor`. The service is the only domain/subdomain validator.
   - Legacy documents missing `subdomain` remain readable and saveable without inserting an empty value; new Web creation requires it. If a newly created draft is edited in author mode before its first successful save, missing `subdomain` is an error; legacy files produce a warning with a jump target.

4. **Exact static scan boundary**
   - `studio-taxonomy-domain-allowlist.v1.json` has `scan_root: "_houkai_merge/AWAKE/tools/worldbook-studio"`.
   - `include_globs`: `src/Awake.WorldbookStudio.Core/**/*.cs`, `src/Awake.WorldbookStudio.Web/wwwroot/**/*.{html,js}`, `src/Awake.WorldbookStudio.Cli/**/*.cs`, `src/Awake.WorldbookStudio.Launcher/**/*.cs`, `新手指引_世界书内容编辑者.md`, `README_使用说明.txt`.
   - `exclude_globs`: `**/bin/**`, `**/obj/**`, `artifacts/**`, `_tmp/**`.
   - Each included production path has four rules (`politics`, `economy`, `culture`, `war`) with `max_occurrences:0`; the two root docs have the same rules. `tests/**`, `scripts/**`, `schemas/**`, `artifacts/**`, and generated output are explicit exclusions with reasons recorded in the fixture. Unknown included paths or unlisted literals fail the scan. Whole-word matching is used.

5. **Taxonomy GET failure wire**
   - On taxonomy failure, `GET /api/editor-catalog` and `GET /api/editor-document` return HTTP 500 and exactly `{ok:false,error:"WB-TAXONOMY-500",message:"分类目录不可用，工作室已停止本次操作。"}`. No file path or stack trace is returned. `EditorFailure` maps `WB-TAXONOMY-500` to 500.
   - The route matrix is: catalog/read/validate/compile/preview/AI-consent → 500 `WB-TAXONOMY-500`; new/save-editor → 500 `WB-TAXONOMY-500` unless CAS is missing (400 `WB-TAXONOMY-CAS-400`) or stale (409 `WB-TAXONOMY-CAS-409`); CLI equivalents return nonzero JSON with the same code. All paths perform validation before any write; compile/export never write outputs on taxonomy failure.
