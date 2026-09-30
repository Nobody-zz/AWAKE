# 世界书 Studio · 操作细则与踩坑备忘

> **类型**：`REF-`（被取代前有效）。**定位＝"怎么跑、踩什么坑"，不是规范。**
> **规范权威**在 `docs/worldbook-studio-plan/*CONTRACT.md`（`TOOLCHAIN-CONTRACT` / `SOURCE-REGISTRY-CONTRACT` / `ID-LEDGER-CONTRACT` / `PERMISSION-CONTRACT` / `RUNTIME-MAPPING-CONTRACT`）—— 本文**不重复**契约，只记**操作层**的硬约束与已踩过的坑。
> **来源**：2026-09-30 由先行线本地工作记忆回流，目的＝**不依赖任何单条 agent 线**也能接着跑（撤线不丢）。
> **维护**：改了 Studio CLI / taxonomy / 部署链之后回来更新。

---

## 一、⭐ CLI 调用硬纪律

**必须在 studio 根跑**（`AWAKE/tools/worldbook-studio`），不能 cd 进 workspace：

```bash
cd "D:/AWAKE-Dev/AWAKE/tools/worldbook-studio"
CLI="src/Awake.WorldbookStudio.Cli/bin/Debug/net10.0/worldbook-studio.exe"
"$CLI" validate --workspace "workspace/full-geo1"
```

- ❌ cd 进 `workspace/...` ⇒ **`WB-SCHEMA-404`**。
- 根因：`FindSchemaRoot(cwd)` 命中 `<studio>/../../docs/worldbook-studio-plan`（因为 `<studio>/docs` 不存在）⇒ `cwd` 必须是 studio 根。
- ⚠️ **schema 双副本**：权威源 `AWAKE/docs/worldbook-studio-plan/`，部署副本 `AWAKE/tools/worldbook-studio/artifacts/current-test/WorldbookStudio/schemas/`（`package.ps1` 从 docs 拷）。**改 docs 后必须同步部署副本**，否则部署侧看不到。

## 二、⭐ Authority 六步链

`register → select → approve → proof → compile --proof`

- **每步必须独立 `operation_id`**；复用 ⇒ `WB-AUTHORITY-OPERATION-409: operation_id 已用于其他操作`。
- ⚠️ **链式编译每批必换 `OPBASE`**，且 `authoring-register-batch` manifest 里的 `operation` 字段**也要带 `OPBASE`**。
  只换 select/approve/proof 的 id 不够 —— **register 才是重读磁盘那一步**。
  症状**是静默的**：编译头哈希全是旧值、表面都"成功"，随后报 `WB-AUTHORITY-CAS-409`。
- ⚠️ 六步耗时 **12–19 分钟**，跑后台；**改档前必确认没有六步链在跑**（后台编译竞态 ⇒ register 读坏文件）。

### 响应字段口径
- **一律 camelCase**（走 `AuthorityPublicProjection`）：`selection.selectionId` / `selection.itemCount`、`approval_proof.approvalId`、`compile_proof.compileProofId`。
- **`compile` 响应顶层没有 `ok`**；**成功判据＝存在 `result_hash`**。
- 写六步脚本前先抄自家已跑通的（`tools/_v2b_chain_20260914.py`、`tools/_v36_polity_full_chain.py`）。

## 三、⭐ document_id 只能按 path 反查

- head 文件 = `<workspace>/authoring-v1/workspace-head.json`，其 `documents` 的 **key 即 `document_id`**。
- **domain 段来自档内 `domain:` 字段，与文件路径无关**：
  `authoring/charas-origin-tales.yaml` → `doc.culture.charas-origin-tales`；
  `authoring/culture-concept-calculating.yaml` → `doc.culture.concept-calculating`。
- ⚠️ head 含**历史残留档**（≥1299 条 vs 实际 789）⇒ **勿全量 select**。

### ⭐⭐ register 的 `--path` 只传「文件名」（09-30 新坑，`WB-DOC-404`）

`Workspace.ResolveAuthoringPath`（`Workspace.cs:448`）三段式：
```
Path.IsPathRooted(path)                       → 原样用
path.StartsWith("authoring", OrdinalIgnoreCase) → Path.Combine(Root, path)
否则                                            → Path.Combine(Root, "authoring", path)
```
⇒ 传 `workspace/full-geo1/authoring/x.yaml`（**相对、但不以 `authoring` 开头**）会被拼成
`<Root>/authoring/workspace/full-geo1/authoring/x.yaml` ⇒ **双拼 ⇒ `WB-DOC-404`**。

- ✅ 正确：`{"operation":"…","path":"politics-polity-senate-body.yaml"}`（纯文件名）
- ✅ 或：`path":"authoring/politics-polity-senate-body.yaml"`
- ❌ 错误：`"workspace/full-geo1/authoring/…"`（把 workspace 前缀也带上）
- ⚠️ 与 §一 的 `WB-SCHEMA-404` **是两个不同的坑**：那个是 **cwd**，这个是 **path 形态**。

## 四、⭐ 引文（证据层）三条硬口径

1. `quote_hash` = **sha256(quote 的 UTF-8 字节)** 小写十六进制（校验点 `Awake.WorldbookStudio.Core/AuthoringDraftContracts.cs:1301`，不符即 throw）
2. `source_content_hash` = 该 source 的 `locator_root` 指向语料 `.txt` 的 sha256
3. `quote` 必须**逐字**出现在语料里；`locator` 必须原样取自 `source_origin`

**引文不进游戏运行时**（`runtime.json` 不含 `quote`/`locator`；`WorldKnowledgeQueryService.cs` grep 零命中）。它是**编译期证据/审计层**：硬门禁 ＋ 界定正文合法边界 ＋ 可追溯链。

### locator 合法形态（5 类）

| 形态 | 例 |
|---|---|
| DB / 原版 xml | `bannerlord.db#localization.XXXX`、`bannerlord.items.xml#name` |
| 聚落描述 | `bannerlord.db#settlements.<id>.descriptionText` |
| chronicle 内 rule | `rules/rule_X__X.json#/Variants/N/Content` |
| 语料文件名开头 | `chronicle-animusforge-geo1.txt 描述后缀` |
| 标注式（无 `#`） | `rule_X__X.json [Vn]` ← 归 P3 提示 |

**`#/Variants/N` 的 N**：语料行自带 `[Vn]` 前缀 ⇒ `N = Vn`（**权威**）；纯文本行 ⇒ `N = 段内相对行偏移(0-based)`。

## 五、⭐ YAML 引号决策树（血泪三次）

| 情形 | 做法 |
|---|---|
| 值以 `& * ! % @ \` [ ] { } # > \| , ? : -` 开头 | **必须加引号** |
| 值含 `\` 转义序列（如 Bannerlord `{\?}`） | **用单引号**（双引号会解析转义 ⇒ `unknown escape character`） |
| 值含 `'` | 用双引号；或单引号内写 `''` |
| **剥引号前** | **必须验证剥后 YAML 仍可解析**（`_fix_quote_wrap.py` 就栽在这，误伤 27 行） |

⚠️ **写「读 YAML 取正文算 hash」的工具时，必须先按 YAML 语义还原引号与转义再算 hash**。
否则会**误把好数据判成坏数据**（`_wb_cite_check.py` 曾因此误报 27 条 P0）——这比漏报更危险，会诱使人去"修"本来正确的文件。

## 六、source_id 易错点

- `source.calradia.chronicle.animusforge`（**geo1 无 `.geo1` 后缀**）
- geo2 / eco / capitals / geography2 / war1 / war3 **各带后缀**
- 沙拉斯湾 = `source.calradia.chronicle.shalas-bay`；卡恰尔半岛 = `source.calradia.chronicle.kachar-peninsula`

## 七、编译产物与基线

- **`compiled/customer/` = 默认输出目录**（`AuthorityGate.cs:469`：不带 `--out` 就跑 ⇒ 写进 `compiled/customer/`，**覆盖上一次**）。
  ⇒ **正式产物一律显式 `--out`**。
- **全量编译 = select 全部正典 id**（`AuthorityGate.MaterializeSelection`：**selection 就是编译集**）。
  范式：列 `authoring/*.yaml`（排除 `_`/`source-`）→ 读每档 `id:` → select 全部 → approve → proof → `compile --out`。
- ⭐⭐ **`--out` 的解析根 = workspace 的 `CompiledRoot`（`<workspace根>/compiled`），不是 studio 根**（`Workspace.cs:63/79 RequireCompiled`）。
  - ❌ `--out compiled/geo1-v40-polity-c`（从 studio 根算）⇒ 落到 `<studio>/compiled/…` ⇒ **不在 workspace 的 compiled 下 ⇒ `WB-PATH-003`**。
  - ✅ `--out workspace/full-geo1/compiled/geo1-v40-polity-c`（cwd=studio 根时的相对写法）；或直接给绝对路径。
  - 实测：v40 首次编译即栽在此，报 `WB-PATH-003 / side_effect:none`（**无副作用，可安全重跑**）。
- ⚠️ **前四步（register/select/approve/proof）成功后，proof 落盘可复用** ⇒ `--out` 之类只影响 compile 的错误，
  修完**只需重跑 compile 一步**（proof 文件 = `authoring-v1/compile-proofs/k1_cp_<sha256(proofId)>.json`，内含 `compile_proof_id` 可核对）。
- ⚠️ **编译门会把被判「回收」的产物整个挪进 `compiled/quarantine/<SafeId(op_id)>/artifact/`**（`AuthorityGate.cs:1258`）。
  ⇒ **报告里写的包路径要当「产物名」看，不是永久地址**；引用前先 `ls`。
- **当前在挂版**：`compiled/geo1-v39-polity-b/`（796 档，09-30 部署）。**具体哈希以 `docs/worldbook-migration/DEPLOY-V39-REPORT-20260930.md` 为准**，本文不复述。
- `validate` 常态基线：`total=0 valid=True`（诊断里 `severity` 全小写 `error`/`warning`）。

## 八、⭐ subdomain 是受控词表

写词表外的 subdomain ⇒ `validate` 报 **`WB-TAXONOMY-422`**（Detail：「"X"下没有这个二级主题」）。

- 词表：`AWAKE/docs/worldbook-studio-plan/knowledge-taxonomy.v1.json`（＋部署副本，见 §一）。
- **三硬约束**：
  1. `taxonomy_version` 是 `const "1.0.0"`，**不可改版本号**（`TaxonomyCatalogService.cs:154/243`）；
  2. **只加条目、不改结构**；subdomain 条目需 4 字段：`id`（pattern `^[a-z][a-z0-9_]*$`）/ `label`（zh-CN）/ `help`（zh-CN）/ `examples`（zh-CN 数组，minItems 1）；
  3. 校验源 = `TaxonomyCatalogService.cs:40 ValidateDocument`。
- 案例：新增 `politics/polity` ⇒ `validate Valid:true Diagnostics:[]`。

## 九、⭐⭐ 部署 = 两步（**只跑第二步＝又搬一次旧的**）

1. **产物 → 仓库侧**：`compiled/<pkg>/` 的 3 文件（`manifest.json`/`runtime.json`/`index.json`）**字节复制**进
   `AWAKE/ModuleData/Worldbook/packages/calradia/`，**并同步仓库侧 registry `manifest.json` 的三哈希**。
2. **仓库侧 → 游戏目录**：`tools/deploy_worldbook_to_game.ps1 -ConfirmDeploy`。

- 契约：入口 = `<祖先>/ModuleData/Worldbook/manifest.json`（`awake.worldbook.registry.v1`）；**包只认 3 文件**，`ReadAndVerify` **不扫目录**；角色卡根 = 含 `persona_definitions/` 那层（**刻意不读 manifest**）。
- **三哈希**：`manifestHash` **不随内容变**（结构性：`packageId`/`version`/`kind`/`entrypoints` 变了才变）；`contentHash`/`packageHash` **随内容变**。**包内大写 / registry 小写**，必须逐条相符。
- ⚠️ **`compile` 返回的 `manifest_hash` = `packages/calradia/manifest.json` 文件的字节 SHA256**，与包内 `hashes.manifestHash` **不是一个东西**。
- ⚠️ 改 `assertions[].text`（YAML 平铺正文）**不进包、不动三哈希、玩家听不到**；玩家听得见的是 `expressions[].text` 与 `summary`。
- **真机确认**：`Modules/AWAKE/Logs/Awake.log` 的 `worldbook_runtime_initialized … package=… entries=`。判两个：① `package=` 是**两段** `awake:worldbook.calradia`；② `entries=` 与刚投的那份包一致。
- dev 菜单 **`worldbook_reload`** 可热重载（不必重启）；`worldbook_status` 看状态。
- 历史时点：448(09-14/15) → 451(09-17) → 482(09-18) → 790(09-30 v37/v38)。

## 十、文件放置

**`authoring/` 下只放正典档。所有备份/归档/退役目录必须放到 `authoring/` 之外。**

- 原因：`Workspace.cs:391 EnumerateAuthoringPaths()` 用 `SearchOption.AllDirectories` **递归扫整个 `authoring/`**；
  过滤器 `IsAuthoringDocument` 只排除 `sources`/`suggestions`/`audit`/`identity`/`draft-state`/`session-state`，
  **不排除 `_` 前缀** ⇒ 放进去就被 validate 扫到，**污染诊断数**。
- 本项目约定：归档放 `workspace/full-geo1/_attic/`。

---

## 附：与既有文档的关系

| 既有文档 | 关系 |
|---|---|
| `worldbook-studio-plan/TOOLCHAIN-CONTRACT.md` | **规范**（Windows 原子发布状态机）；本文是**操作层补充** |
| `worldbook-studio-plan/{SOURCE-REGISTRY,ID-LEDGER,PERMISSION,RUNTIME-MAPPING}-CONTRACT.md` | 各自领域的**规范**；本文只记操作坑 |
| `worldbook-migration/DEPLOY-V*-REPORT-*.md` | 每次部署的**实测读数**（哈希以它为准） |
