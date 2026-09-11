# Plan Review Log: Worldbook Studio 五大知识分类与二级主题

Act 1 (grill) complete — plan locked with the user. MAX_ROUNDS=3.

## Round 1 — Independent read-only review

VERDICT: REVISE

主要问题：旧运行时四类合同与 Studio authoring 分类边界未区分；分类目录缺少稳定契约、版本哈希和保存快照；`subdomain` / `related_domains` 的机器约束、AI 投影和负向测试不完整。

## Claude's response

已将本批次收窄为 Studio authoring/catalog 与当前 v2 编译链：明确不修改旧 `tools/worldbook-contract/v1`、周报/事件合同、旧运行时枚举、游戏目录和冻结候选。补充了分类目录 Schema、版本/哈希/CAS、字段稳定 ID 与跨领域规则、旧文档兼容矩阵、地理档案完整离线链路和 AI 投影边界。

## Round 2 — Independent read-only review

VERDICT: REVISE

主要问题：当前 v2 runtime contract 实际位于 `tools/worldbook-contract/v1` 且仍是四类；taxonomy 源路径、raw-byte hash/CAS、字段接线、AI request schema、运行时 extensions、测试 ID 和 Studio 四类静态白名单仍未足够具体。

## Claude''s response

已明确该目录承载当前 v2 runtime contract，并把本批次限定为只扩展知识域枚举；明确 taxonomy 源文件、配套 Schema、发布路径、raw UTF-8 hash、CAS 字段和错误码；补充新建/编辑/API/模板/AI projection/运行时 extensions 接线、冲突归类表、兼容矩阵、地理输出断言、测试 ID 与 Studio 四类静态白名单。周报和事件四类保持不变。

## Round 3 — Independent read-only review

VERDICT: REVISE

剩余问题：共享 common schema 直接放宽会误伤事件/周报；runtime extensions 尚无受约束字段；TAX-008 白名单、CAS wire 字段和 taxonomy 缺失/版本失败行为仍需落名。

## Claude''s response

已拆分 `knowledge_domain`（五类）与 `event_domain`（四类），让 runtime/知识条目和事件/周报引用不同定义；固定 runtime `entries[*].extensions` 字段与 Schema 约束；固定 `studio-taxonomy-domain-allowlist.v1.json`、扫描根、允许计数；固定 catalog/editor-document/save 请求的 taxonomy 字段、CAS 响应和 fail-closed 错误矩阵。

## Round 4 — Independent read-only review

VERDICT: REVISE

剩余问题：common schema 需要完整 `$ref` 替换矩阵；entry extensions 需要精确字段/兼容边界；必须列出五类二级 ID；JSON Schema 与 taxonomy 顺序需区分外部校验；TAX-008、CAS envelope、fail-closed 入口矩阵和源码/包副本语义仍需落名。

## Claude''s response

已补充完整 taxonomy ID 与顺序、冲突归类样例、knowledge/event `$ref` 矩阵、entry 专用 extensions Schema、旧根级/表达级扩展兼容、Schema 与 Core 的职责边界、源码与发布副本及 manifest hash 规则、精确 CAS 请求/响应、taxonomy 缺失错误矩阵、新建两条 API 分支、AI snake_case 投影和静态扫描白名单契约。

## Round 5 — Independent read-only review

VERDICT: REVISE

剩余问题：需补齐 common.schema 内 knowledge_entry 与 authoring schema 的精确引用、旧 entry extensions 的兼容策略、所有 authoring 路径复用同一校验器、TAX-008 的实际规则和扫描范围，以及 catalog/document GET 的 500 envelope 和逐入口 fail-closed 行为。

## Claude''s response

已补齐完整 `$ref` 替换矩阵；明确 entry extensions 对未知旧字段保持兼容、taxonomy 保留字段严格校验和空数组语义；规定所有读取/保存/新建/导入/编译/预览/AI/CLI 路径复用单一 TaxonomyCatalogService；给出 TAX-008 逐路径、逐 literal、max_occurrences 规则；明确两个 GET 的 500 JSON envelope 与全入口错误/写入矩阵。

## Round 6 — Independent read-only review

VERDICT: REVISE

剩余问题：需要把 common/runtime/authoring/event 的最终 `$ref` 字符串、entry extensions 的兼容选择、TaxonomyCatalogService 的所有调用点、TAX-008 固定 scan_root/排除范围和 GET 500 envelope 写成覆盖前文的唯一绑定决议。

## Claude''s response

已追加 Binding resolutions，明确前文冲突以本节为准：锁定 Studio authoring Schema、current v2 common/runtime/authoring Schema、event/weekly Schema 的精确引用；选择 entry extensions 保留未知旧字段；列出所有 Core 调用路径；固定静态扫描根、include/exclude；固定 catalog/document GET 的 500 envelope 和全入口写入边界。
