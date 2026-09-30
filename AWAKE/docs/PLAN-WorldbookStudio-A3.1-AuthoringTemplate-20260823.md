# Plan: Worldbook Studio A3.1 作者档案模板构造 seam

> 状态：**`implemented`（2026-09-30 标记）。**
> 依据：本批所属子系统已存在：`tools/worldbook-studio/` 含 `Awake.WorldbookStudio.{Cli,Core,Launcher,Web}` 与 entity registry；本系列为该子系统的内部行为保持型重构批次。
> ⚠️ 本文件是**批次施工单**，其所属子系统已存在 ⇒ 本批视为已完成。**保留本文件**（不是 `superseded`）——
> 它记录了当时的设计意图与边界，仍有追溯价值。
> 现行方向：`AWAKE-ROADMAP.md`；分诊依据：`AUDIT-PLAN-TRIAGE-20260930.md`。

_Locked via grill — by Codex + user autonomous execution authorization; revised after independent read-only review_

## Goal

在不改变世界书 Schema、`CreateDocument` public 行为、生成 JSON/YAML 的完整字段顺序、registry binding、错误码、revision、默认权限和任何 CLI/Web 契约的前提下，将 `WorldbookApplicationService.BuildAuthoringTemplate` 从单文件 application service 中抽离为一个唯一的纯模板构造 seam，降低已确认的静态嵌套复杂度，并用独立固定的 characterization golden 证明抽离前后生成结果完全一致。明确区分：canonical hash 只证明规范化内容，不证明 `JsonObject` 插入顺序；顺序必须由独立完整树断言证明。

## Approach

1. 记录当前基线：`MakeWorkspace` 的 registry 输入明确来自 `docs/worldbook-studio-plan/profile-registry.v1.json` 与 `docs/worldbook-studio-plan/referral-registry.v1.json`，运行时路径为 `Path.Combine(schemaRoot, "profile-registry.v1.json")` 和 `Path.Combine(schemaRoot, "referral-registry.v1.json")`；固定版本 `1.0.0`、profile 字节 hash `309d10583e7b4af05f28de2c2e67a2528cb3d6c45752369c0f7d160fabc3f3a5`、referral 字节 hash `6e17075f1f967be28c0437121db50e5c30809c4efc3d2a5f22ad9bb0b3b926cd`。固定精确输入：`doc.politics.a3_1_golden`、标题前后含空格的 `  A3.1 模板基线  `、`politics`、`base`、`author.developer`，并覆盖空标题默认值、`.yaml`、`.json`、大写 `.JSON`、无扩展名/未知扩展名、嵌套路径和已有文件覆盖语义。golden 测试先断言上述两个实际文件的运行时字节 hash，再使用它们。
2. 新增 `src/Awake.WorldbookStudio.Core/AuthoringTemplateFactory.cs`，只承接原 `BuildAuthoringTemplate` 的对象构造逻辑；保留原有字段插入顺序、中文占位文案、`profile.commoner` 默认 grant、`local` scope、`summary` detail 和 registry hash/version 注入规则。类型为 `internal`，方法只接受字符串值和 `RegistrySnapshot`，不得引用 `WorkspaceService`、`File`、`Directory` 或任何 I/O/全局状态。
3. 在 `Application.cs` 仅把 `CreateDocument` 的单一调用改为 `AuthoringTemplateFactory.Create`，删除旧私有构造方法；保留标题 trim/default、document ID/domain/content tier/author ID 校验、slug 派生、registry `LoadAndValidate` 先行、格式选择、保存、重新读取和错误边界。
4. 新增受版本控制的 `tests/fixtures/a3-1-authoring-template-golden.v1.json`，固定上述输入、registry 前置 hash/version、预期 canonical hash、完整有序属性路径树、数组长度/顺序、关键值和无诊断结果，并固定独立人工确认的 `json_property_order`、`yaml_mapping_order` 和相应文本顺序断言；golden 不得由当前实现运行时重生成。测试明确说明 canonical hash 不覆盖对象插入顺序。
5. 在现有单一测试 harness 中只增加 4 个 named cases（总数由当前 `83` 变为 `87`，入口、输出格式和失败行为不变）：
   - A3.1 模板 golden：比较完整对象树的 key enumeration 顺序、数组顺序、所有 golden 路径和值、canonical hash，并将 JSON 序列化文本的属性顺序和 YAML 序列化映射顺序分别与 golden 中的独立预期序列/文本位置比较。
   - A3.1 YAML/JSON：分别创建 `.yaml`、`.json` 和大写 `.JSON`，重新读取完整对象、canonical hash、schema diagnostics 与 diagnostics 顺序完全一致；同时覆盖标题 trim/default 与嵌套 ID。
   - A3.1 CreateDocument 负向边界：覆盖既有 `WB-DOC-001..005` 的异常代码/先后边界；不声称能从 `CreateDocument` 注入 schema/save/read 故障，也不新增 fault-injection seam，只保留成功保存后重新读取/校验的现有路径回归。
   - A3.1 路径/registry 边界：仅复用既有 `WorkspacePathPolicy` 行为，覆盖空路径 `WB-PATH-001`、authoring 外部路径 `WB-PATH-003`、禁止段 `WB-PATH-004` 和未知扩展 `WB-SAVE-001`；覆盖 `authoring/nested/...` 成功、已有文件被原子替换为新文档、路径策略失败时既有文件不变；证明 registry 不可用在工厂前失败、目标文件不写入，且四个 binding 值严格等于固定 registry snapshot。
   既有 F32、A1 `72/72` 和 A2 快照测试保持不变。
6. 在实现前后对现有 harness named-case 清单做机器核验：清单定义为测试源文件中所有匹配正则 `^Run\("` 的完整源代码行，按源文件顺序取出，使用 UTF-8 编码，以单个 LF 连接，且不含首尾换行。实现前共 `83` 行，基线 SHA-256 为 `d8035a0fd6e13027589e6a0e84a39cf8474967d77ef11a6bef65d4ec1595729b`；实现后从当前清单移除且仅移除 4 个新 A3.1 名称，再比较该基线 hash，并另断言总数 `87`。这项核验不改生产代码、不引入第二测试入口。
7. 运行 focused harness、Release build、现有只读 A1 CLI/Web smoke 和范围债务审计；smoke 不改生产代码、路由或 fixture。仅在所有结果通过后归档 A3.1 checkpoint，未打包、不启动游戏、不同步运行时目录。

## Key decisions & tradeoffs

- 选择 `BuildAuthoringTemplate` 而不是 `BuildContentGraph`、`Preview` 或 `ValidationServices`：它是静态审计中确认的高嵌套纯构造块，调用方只有 `CreateDocument`，风险和验证面最小。
- 新 seam 保持内部静态工厂，不引入 Mediator、Repository、DI、缓存或新的 public API；避免为一次构造过度抽象。
- 保留 `Application.cs` 作为 public application service 权威入口；工厂只负责返回未保存的 `JsonObject`，不读取磁盘、不验证 registry、不写文件。
- golden 同时比较 canonical JSON hash、完整有序树、JSON 文本顺序和 YAML 映射顺序；hash 仅证明排序后的内容，不能替代顺序断言。不把运行时生成时间或路径绝对值引入结果，确保跨机器可复现。
- Registry snapshot 的加载与失败优先级仍由 `CreateDocument` 管理；工厂只消费已验证 snapshot。固定 registry 文件路径和 bytes hash 作为 golden 前置条件，避免 registry 更新伪装成 seam 回归。
- 已有文件的成功语义是按 `WorkspaceService.WriteAuthoringTarget` 的现有临时文件→替换路径原子覆盖；路径策略拒绝时在写入前失败，原有目标内容保持不变。本批不制造不可达的人工 I/O fault。
- 测试新增 4 个 `Run`，明确 harness 从 `83/83` 变为 `87/87`；A1 保护矩阵仍严格为 `72/72`，两者不可混为同一计数。
- 本批只报告静态复杂度风险的结构性下降，不宣称已经测得性能收益。

## Acceptance contract

- **Trigger:** 调用 `WorldbookApplicationService.CreateDocument`，分别使用 YAML、JSON 和大写 `.JSON` 扩展名创建固定输入。
- **Observable result:** 创建的档案可重新读取并通过现有 schema；canonical JSON 与 golden hash 相同；完整有序 key tree、数组顺序、全部关键值、工厂对象顺序、JSON 文本属性顺序、YAML 映射顺序、YAML/JSON 解析对象、canonical hash、schema diagnostics 和 diagnostics 顺序与 golden/彼此一致；JSON/YAML 文本顺序分别与 golden 的独立预期序列匹配；默认权限、registry binding、ID、revision、状态和中文占位文案不变。
- **Invariants:** 不新增或改变 public CLI 命令、HTTP 路由、错误码、诊断顺序、Schema、输出 hash、A1/A2 fixture 和运行时目录；工厂本身无 I/O，且源码不引用 Workspace/File/Directory。
- **Failure behavior:** 原有 `WB-DOC-001..005`、`WB-PATH-001/003/004`、`WB-SAVE-001` 失败路径保持原错误码、触发先后、异常边界和“失败不写目标”的语义；registry 不可用必须在工厂调用前失败；成功创建仍必须通过原有 save→read→schema 路径；不引入不可达故障模拟。
- **Evidence:** A3.1 named harness `87/87 PASS`；existing-case manifest baseline hash check passes (`83` preserved plus exactly 4 new cases)；A1 smoke `PASS`；Release build `0 warnings / 0 errors`；债务审计 `passed`；源码 diff 仅限计划文件、工厂、调用点、golden、测试和 checkpoint。
- **Non-goals:** 不重构 content graph、preview、ValidationServices、Publisher、编辑器 UI、AI/CAS、Launcher、Provider、Worker、世界书 Schema 或游戏运行时；不改变 WorkspacePathPolicy 或新增 fault-injection infrastructure。

## Risks / open questions

- `JsonObject` 插入顺序若被无意改变，canonical hash 可能仍不变；必须以完整树的 key enumeration、JSON 文本属性顺序和 YAML 映射顺序三层断言阻止这种回归。
- YAML/JSON 不要求文本逐字相同；必须比较重新读取后的完整对象、canonical hash、schema diagnostics 和 diagnostics 顺序，避免标量格式差异被误判为无关。
- 旧测试 harness 是单文件顶层程序；named-case manifest 核验必须只比较现有 83 行基线与新增 4 行，不改变入口、输出格式或失败报告格式。
- 本批只证明离线代码与 Studio 契约，不证明 Bannerlord 实机、云端 Provider、本机 Worker、存档或游戏目录同步。

## Change budget

- Production: 1 new Core file plus one existing call-site deletion/replacement; no more than 1 new internal pure factory type and 1 method.
- Tests/fixtures: 1 golden fixture plus exactly 4 focused harness cases and one read-only named-case manifest check; no unrelated fixture rewrites or CLI/Web production changes.
- No package rebuild or artifact overwrite in A3.1.
