# corrections_20260914 · classification v2 落地

## taxonomy v2（两处内容变更）

文件：`docs/worldbook-studio-plan/knowledge-taxonomy.v1.json`
版本：**1.0.0 不变**（schema `const` 与 C# `SupportedVersion` 均硬编码 1.0.0；内容变更靠包 hash/manifest.taxonomyHash 识别）

### 变更 1：geography 恢复原 9 子域
- 撤（48 版自造）：`mountain` / `plateau` / `peninsula` / `desert` / `waters` / `castle` / `village` / `town`
- 恢复：`terrain`（地形）／`rivers`／`settlements`（聚落）
- `rivers` 微调：label「河流」→「水域」，help「河流、湖泊、渡口和水域通行」→「河流、湖泊、海湾、海域、渡口和水域通行」

### 变更 2：culture 的 `tale` 撤回 `faith`
- 撤：`tale`（48 版自造）
- 恢复：`faith`（信仰），help「神祇、教义、仪式和宗教组织」→「神祇、教义、仪式、宗教组织与民间怪谈」

### war 的 `military_system` → `military`
已在 48 版完成，v2 沿用。

## 448 档（两个目录同步）
- `tools/worldbook-studio/workspace/full-geo1/authoring/`
- `docs/worldbook-migration/projection/authoring-out/`

| 动作 | 档数 | 说明 |
|---|---|---|
| 改名 | 8 | 7 水域（`waters-*` → `bay-charas` / `lake-lakonis` / `lake-llyn-modris` / `river-miron` / `sea-perassic` / `river-sethys` / `lake-tanaesis`）＋ 1 矿场（`resources-lycaron` → `mine-lycaron`） |
| 改 subdomain 字段 | 406 | 393 聚落 → `settlements`；7 地貌 → `terrain`；6 `tale` 分挂 `faith`×2／`customs`×2／`arts`×1／`identity`×1 |
| 不动 | 34 | items 15／goods 6／territories 4／throne 2／troops 2／weapons 1／military 3／clans 1 |

**注**：393 聚落 + 7 地貌 + 6 tale + 34 不动 + 8 改名（其中 7 水域已计入字段改动）合计对齐 448。

## 验收
- `validate --workspace tools/worldbook-studio/workspace/full-geo1` → `Valid: true`，零诊断（InputHash `C029734A…`）
- 前缀 ↔ 子域：18 组全对角线（castle/village/town→settlements，mountain/plateau/peninsula/desert→terrain，lake/river/bay/sea→rivers，mine→resources）

## 踩到的三个坑（记下防复发）
1. **`supportedVersion` 硬编码**：改 `taxonomy_version` 会撞 `TaxonomyCatalogService.SupportedVersion = "1.0.0"` 与 schema `const`，直接 `WB-TAXONOMY-500`。「内容改但版本不动」是本项目惯例。
2. **子域不允许 `conflict_hints`**：schema `$defs.subdomain` 是 `additionalProperties:false`，只许 `id/label/help/examples` 四键（域级才有 conflict_hints）。加了就 500。
3. **Windows 文件写入瞬时 EINVAL**：批量改名时 `[Errno 22] Invalid argument` 偶发（疑杀软/索引器锁），加 retry + 退避即可；**不要**误判成路径过长。
4. **诊断脚本自身 bug 更易误导**：用 `elif` 解析 id/subdomain 时写错分支，报出 448 档全"缺字段"的假警报；**改脚本后先用 `splitlines()` 笨办法交叉验证**。

## 已完成（12:2x–12:5x 收口）

### 1. 补登记（无需整批重登记 448 档）
**关键修正**：原以为要重登记 448 档（≈90 分钟），实测**不必**。判据＝09-13 17:58 造的 448 项 selection 快照逐项对磁盘：
- **414 项** path + content_hash 与磁盘完全一致（taxonomy 未动的档）
- **34 项** path 已失效 = v2 改名/改 id 的那些 ⇒ 以「新文档」补登记
- 34 档补登记实测 **34/34 ok，耗时 513 秒**（≈17 秒/档）

```
_need34.txt            ← 补登记清单（34 条，doc_id + path）
_supplement_register_v2_20260914.py   ← 批量补登记（独立 op id / 档）
_supplement_reg_done.json / _supplement_reg_log.txt
```

### 2. 修 CAS-409（第二次踩到同类坑）
select 后 compile 报 `WB-AUTHORITY-CAS-409: CompileProof 输入已变化`。
根因：v2 改了 **10 档**的 `subdomain`，但它们的 `revision:` 字段**没变** ⇒ `RegisterDocument` 写的是
`{file_revision}.json` ⇒ 同名覆盖成旧内容，登记记录的 `content_hash` 仍指旧值。
其中 1 档（`mine-lycaron`）因改 id 另获新记录，故实际失配 **9 档**。

修法：对这 9 档用新 op id **重新 register**（`v2.reregister.*`）。
⇒ **教训：改档内容字段后，若 `revision:` 不动，必须重跑该档登记**。

```
_fix_cas409_20260914.py / _fix_cas409_log.txt
```

### 3. 编译产出（新包）
```
_v2b_chain_20260914.py  select → approve → proof → compile
```
- selection `selection.f16a64f601694a1bbb2e524006063c09`（**448 项**）
- compile proof `compile.db7d6c97d2d144d688a96b42177ccc13`
- 产出 `compiled/geo1-v2/`：**documents 448**、`Valid: true`
- hash：`manifestHash 3A3CF05A…`（与旧包一致）、`packageHash 76984293…`（新）、`contentHash A1CC5706…`
- 诊断 **21 条，全部 `warning` / `WB-INDEX-AMBIGUOUS`**（既有重名提示，与 v2 无关）
- 子域分布：settlements 393／items 15／rivers 7／terrain 7／goods 6／territories 4／military 3／
  faith 2／customs 2／throne 2／troops 2／arts 1／identity 1／resources 1／clans 1／weapons 1

### 4. 重建 Studio 包
- `package.ps1` 拷 `docs/worldbook-studio-plan` → 包内 `schemas/`，taxonomy 随包
- 包目录 `artifacts/current-test/WorldbookStudio/` + `WorldbookStudio-win-x64.zip`（617 条目 / 127 MB）
- **包内 taxonomy sha = `9F720A63…`（v2 版）**，`SHA256SUMS.txt` 与 `manifest.json` 均已同步

### 5. 钉住的哈希同步（`a4-cli-web-contract-golden`）
`tools/worldbook-studio/tests/fixtures/a4-cli-web-contract-golden.v1.json:251`
`8bdf0666…` → **`9f720a63…`**；改后与全 24 个 schema 文件逐一比对**全绿**（修复前只有 taxonomy 一处 DIFF）。

### 6. 名录 / 分类表按 v2 重生成
- `WORLDBOOK-CLASSIFICATION-REGISTRY-20260914.md` + `_classification-flat.v1.json`
  （448 档 / 5 域 / 43 子域，在用 16、未用 27 / 21 前缀）
- `WORLDBOOK-NAME-INDEX.xlsx`（448 行 / 5 分类 / 16 在用子域 / 21 前缀）

**脚本修补**：`_gen_classification_registry_20260914.py` 的 `PREFIX_ZH` 词表原是 48 版旧前缀
（`mount`/`mountains`/`furs`/`item`/`territory`/`clan`/`troop`/`weapon`），已按现场 21 个在用前缀对齐；
`FIXES` 四项归类欠账**全部由 v2 消除**，改为空表。

## 未做（本轮不动，如实记录）

### A. `A3.3 preview golden` —— 本仓库**一直**失败的既有坏账
- 症状：`FAIL A3.3 preview golden: baseline_commoner input file closure/hash changed`
- 判据：golden（`tests/fixtures/a3-3-preview-golden.v1.json`）的 `input_files` 里 16 条，
  **14 条与当前仓库一致，唯一 DIFF 的是 taxonomy**；而它钉的 `8BDF0666…`
  **相对 git HEAD（`6571AE15…`）也不符** ⇒ 该 golden **在本仓库从来没对上过**
- 根因：golden 的 `baseline_capture.capture_command` 与全部路径写死指向
  `../OneDrive/文档/New project/_houkai_merge/AWAKE/…` —— 它是**在另一个仓库副本里生成的跨副本快照**
  （那个副本的 taxonomy 恰是 `8BDF0666…`，已核实）
- **正确修法＝在本仓库用 capture 流程整份重生成**（9 个 case × envelope/items/diagnostics/input_files 等十余字段），
  不是改一条哈希。**只改 taxonomy 一条会让后续 envelope 断言继续炸，是假修。**
- 影响：`package.ps1` 的 `release-check` 会在 `test.ps1` 这关抛 `WB-RELEASE-020` 中断
  ⇒ 包目录与 zip 已用等价方式产出（见上 §4），但**正式 release 闸口仍红**。

### B. `production-smoke` 的 `CS1069`
`WorldbookRuntimeProductionSmoke.csproj:24` 用 `<Compile Include="..\..\src\**\*.cs">` 通配引入 src 全部 .cs，
而 `src/AwakeImageClient.cs`（`??` untracked、**非本轮改动**）用 `HttpClient`，工程引用清单无 `System.Net.Http`。
**与 taxonomy v2 无关**，属他人未完成工作。

### C. 待用户拍板 3 点（v2 稿 §七）——**已于 21:0x 拍板并落地，见下一节**

---

## 第二轮（21:0x–收口）· 命名统一三项

用户裁决原文：「**1保留，但范围得改，2文件名看不出实际内容 3统一一下用复数**」，
补充裁定：faith 的「民间怪谈」**归 `arts`**；tale **先看对照再定**；复数**实体类加 s、抽象类不动**。
tale 命名二次裁定（否掉「后缀」思路）原话：
> **「根本不是后缀的问题，是你用简短词汇概括内容的时候没做好」**

### 1. faith 措辞（`knowledge-taxonomy.v1.json`）
- `faith` help：「神祇、教义、仪式**与民间怪谈**。」→「神祇、教义、仪式和宗教组织。」
- `arts` help：「歌曲、故事、舞蹈、绘画和工艺。」→「歌曲、故事、**怪谈**、舞蹈、绘画和工艺。」
  （怪谈从 faith 移出后必须有落点，否则成无主项）
- `taxonomy_version` 保持 `1.0.0`；过 `knowledge-taxonomy.v1.schema.json`；子域无 `conflict_hints`。
- 哈希 `9F720A63…` → **`66352747…`**，已同步 `a4-cli-web-contract-golden.v1.json`（24 个 schema 文件一致）。

### 2. tale 族改名（4 档，改为「地点＋事由」）
| 旧档名 | 新档名 | 中文标题 |
|---|---|---|
| `tale-kachar-three` | `tales-kachar-rulership` | 卡恰尔半岛·归谁所有 |
| `tale-charas-origin` | `tales-charas-origins` | 沙拉斯湾·三种来历 |
| `tale-lakonis-lake` | `tales-lakonis-red-water` | 拉科尼斯湖·水为何变红 |
| `tale-lycaron-rock` | `tales-lycaron-vultures` | 吕卡隆·石山秃鹫 |

`tales-dawn-taboo`（黎明山脉·禁忌与传说）、`tales-husn-fulq`（富勒格其人与"鬼点子"）**判定合格，不动**。

被否掉的方案留痕（供后人别再绕）：`-three`（只表数量）／`-accounts`（像会计账目）／`-as-told`（元描述）——**都是在描述"处理方式"而非"是什么"**。

### 3. 前缀复数化（414 档改名）
- 实体类 13 种加 s：`villages`(273)、`castles`(67)、`towns`(53)、`tales`(6)、`mountains`(4)、`lakes`(3)、`rivers`(2)、`bays`・`deserts`・`mines`・`peninsulas`・`plateaus`・`seas` 各 1。
- 抽象类 `military`／`throne` **不动**；已复数者（`items`／`goods`／`clans`／`territories`／`troops`／`weapons`）不动。
- **范围＝B 案**（用户选）：**文件名＋`doc` id 首段**用复数；`assertion`／`expr` id 起始段**保留历史单数形态**
  （如 `assertion.castle-ab-comer-castle-1`），不跟着复数化。
- 结果：21 前缀（13 复数 ＋ 8 不动），448 档，两镜像目录逐字节一致。

### 4. 本轮踩的三个坑（同一个根因：**裸字符串全局替换越界**）
1. **命中专名同形词**：`castle-ab-comer-castle` 词尾 `castle` 是地名（"Ab Comer Castle"）的一部分，
   被复数化成 `castles` ⇒ id 成 `assertion.castles-ab-comer-castles-1`。**67 个 castle 档全中**
   （游戏城堡英文名恒为「XXX Castle」，尾词必是专名）。
2. **命中 `aliases`**：改 title 时 `replace("Kachyar Peninsula", "Kachyar Peninsula: Whose Land It Is")`
   把 `aliases.en` 那条也换了 ⇒ 值含 `: ` ⇒ **YAML 解析成映射**（合法 YAML，但不是字符串）
   ⇒ schema 报 `additionalProperties: ["en"]` ＋ `type: object should be string`。3 档中招。
3. **改写 title 时丢了原有单引号** ⇒ `WB-YAML-001`（中/英标题含 `: ` 必须带引号）。

**修复与验证方法（可复用）**
- `tools/` 被 gitignore、**没有 git 可回滚**；但 `authoring-v1/document-revisions/<doc_id>/<rev>.json`
  存着**登记当时的完整 `content`**，是天然 baseline。逐档 diff 反推旧 doc_id → 取账本原文 → 行级比对，
  差异分「预期（id/title 行）」与「误伤」。结果：**447 档只有预期差异，3 档误伤**（即坑 2），修完全工作区 `Valid: True`、0 诊断。
- 三级验证（缺一不可）：① PyYAML 全量 lint（抓语法）；② **最小空工作区探针**跑 `validate`（抓 schema；
  注意 `validate` 的 `--path` **是无效参数**、恒校验全工作区，直接跑会被淹没）；③ 账本 diff（抓语义越界）。

### 5. 本轮产物
- 全量重登记 448 档（`_v2c_register_20260914.py`，约 6 秒/档）。
- 编译链 `_v2d_chain_20260914.py`：select(448)→approve→proof→compile，`--out` ＝
  **工作区相对全路径** `tools/worldbook-studio/workspace/full-geo1/compiled/geo1-v3`（`--out` 按进程 CWD 解析）。
- 分类名录 `WORLDBOOK-CLASSIFICATION-REGISTRY-20260914.md` ＋ `_classification-flat.v1.json` 重生成（`PREFIX_ZH` 词表已改复数键）。
- 名录 `WORLDBOOK-NAME-INDEX.xlsx` 重生成（448 行 / 21 前缀 / 6 个 tale 新名）。

### 6. 编译产出（`compiled/geo1-v3`）
- selection `selection.4c46b7a5998345d6bcd4d58469309cc3`（**448 项**）；compile proof `compile.8ed5bdf2895d4dfdb91478d4f607524b`。
- **documents 448、`Valid: true`、0 error / 21 warning**。warning 全是既有的 `WB-INDEX-AMBIGUOUS`
  重名提示，条数与 geo1-v2 **完全相同** ⇒ 本轮未引入新问题。
- hash：`manifestHash E1E68003…`、`contentHash 8D8E07F2…`、`packageHash 630DDD2D…`、`result_hash 284B01A4…`。
- CLI 事实：`compile` 的响应**没有 `ok` 键**，成功判据是存在 `result_hash`；字段名一律 **camelCase**
  （`selection.selectionId` / `approval_proof.approvalId` / `compile_proof.compileProofId`）。

### 7. 包内 taxonomy 同步（Studio 包）
- 问题：`scripts/package.ps1:81` 把 `docs/worldbook-studio-plan` **整目录**拷进包内 `schemas/`；
  21:0x 改了 taxonomy ⇒ 包内那份（`9f720a63…`）相对仓库（`66352747…`）**已过期**。
- **未跑 `package.ps1`**：它的 `release-check` 会先跑 `test.ps1`，而 A3.3 golden 是既有坏账（见 §A）
  ⇒ 必然 `WB-RELEASE-020` 中断，与本次改动无关。改用**定向刷新**：只换
  `schemas/knowledge-taxonomy.v1.json` → 重算 `manifest.json` / `SHA256SUMS.txt` → **从包目录**重建 zip。
- 结果：包内 taxonomy `66352747…`；manifest 613 条**逐条对磁盘哈希** 0 不符、0 未登记文件；
  `SHA256SUMS.txt` 613 行与 manifest 路径集合一致；zip **615 条目 == 磁盘 615 文件**。
  另核（release-check 可独立执行的部分）：batch 合同哈希 `df19cc67…`、authoring schema 源↔包、
  `studio-draft.js` 源↔包、9 个必需文件齐全 —— **全绿**。
- 备份：`artifacts/archive/2026-09-14/current-test-pre-taxonomy-v3/`（旧 taxonomy＋旧 manifest＋旧 SHA256SUMS＋旧 zip）。

#### 7.1 这一步踩的坑（**PowerShell 替换串 `$` 紧跟数字**）
`.NET Regex.Replace` 用 `'$1' + sha + '$2' + len` 会坏：`$1` 后**紧跟** sha 首位数字 `6`
⇒ 引擎把 `$166352747…` 读成一个非法超长组号 ⇒ **整段按字面量输出**，manifest 里被写成
`$1<sha>$218722` ⇒ `ConvertFrom-Json` 报「传入的对象无效」。
- 治本：用 `\g<1>`（Python `re.sub`）或 PowerShell 的 `${1}`；**永不用裸 `$1` 紧跟数字**。
- 附带坑一：第一版 zip 是**从旧 zip 拷条目**建的 ⇒ 新 zip 里塞的仍是**旧 manifest / 旧 SHA256SUMS**。
  **重建 zip 要以「包目录」为源，不以「旧 zip」为源。**
- 附带坑二：旧 zip 含 2 个**目录条目**（`schemas/mappings/` 等，Compress-Archive 风格）；
  `package.ps1` 只放文件 ⇒ 新 zip 无目录条目，反而更贴近官方产物。

#### 7.2 附带观察（未裁）
universe 的 `manifest.displayName` 取自**编译输入里第一篇文档的 title**
（`RuntimePackageCompiler.cs:15,92`：`documents.FirstOrDefault().Document["title"]`），不是世界名。
⇒ 改名会让它漂：v2 是 `Charas Bay: Origin Tales and Sailor Lore`，v3 成 `沙拉斯湾·三种来历`。
`worldId`（`awake:world:awake_current`）取自同一篇的 `universe` 字段，因各档该字段一致而未变。

### 8. 仓库侧包形态落地（22:3x–22:5x）

> ⚠️ **本节留痕，勿照抄（2026-09-15 追注）**：下面写的 `packageId=awake:worldbook:calradia` 是**三段**，
> 违反 `package_id` 的**两段** pattern（`^[a-z][a-z0-9_-]*:[a-z][a-z0-9_.-]*$`），Studio 测试 `F24` 判红。
> **现行正确值＝`packageId=awake:worldbook.calradia`**（`worldId` 是 `stable_id`，三段不变）。
> 缘由与修法见 `RUNTIME-MAPPING-CONTRACT.md`〈身份定名〉的 09-15 纠注。

- **§7.2 那条已裁**：身份不再从文档推 ⇒ `packageId=awake:worldbook:calradia`、
  `worldId=awake:world:calradia`、`displayName={zh-CN:卡拉迪亚,en:Calradia}` 改为
  `RuntimePackageCompiler` 里的**常量**（448 档一个字不改、不重登记，只重编译）。
- **重编译**：新 op 批次 `v2e.*`（避开 `v2d.*` 幂等态）→ `compiled/geo1-v4`，
  448 docs、`0 error / 21 warning`（**与 geo1-v3 逐条相同**）、`manifest_hash 8a0d56f1…`。
- **组装**：新脚本 `tools/assemble_worldbook_package.ps1` 落 `AWAKE/ModuleData/Worldbook/`
  （registry ＋ `packages/calradia/` 三件套，逐字节拷贝、三 hash 与 registry 逐条相符）。
- **⚠️ 新发现的第四方＝`.gitignore`**：`AWAKE/ModuleData/Worldbook/*` 整条排除（缘由写"v1 已被取代"），
  形态定得再对也**进不了库**。已放行 `manifest.json` ＋ `packages/`；v1 内容仍排除。
- **主干两处断口**：`sync_module.ps1` 受管清单**没有 `packages`**（包投不出去）＋ 八条 v1 目录是死配置；
  `Assert-SourceManifest` 已重写（不再向 manifest 要 v1 的 persona 字段，改为断言 canonical 路径 ＋
  校验 registry 每个包的三件套）。
- **验证**（含**变异检验**，见技能 §4）：smoke 新增 `TestRepositoryPackageForm` 直读真实仓库包；
  改坏 runtime 一处 `summary` ⇒ 红 `WB2-HASH-MISMATCH:content`，还原 ⇒ 绿；`sync_module` 测试 15/15。

#### 8.1 这一步踩的两个坑
- **`[IO.Path]::GetFullPath` 不认 PowerShell 的 location**：组装脚本第一版用 `Get-FullPath` 解析
  相对 `-PackageRoot`，`Set-Location` 后仍被解析到**上一层**（`.NET` 进程 CWD≠PS location）
  ⇒ 症状是"PackageRoot is missing: D:\AWAKE-Dev\tools\…"。
  治本＝`$ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath()`。
- **递归删除被环境 fail-closed 拦下**：原脚本用 `Remove-Item -Recurse -Force` 清 `packages/`，
  被 safe-delete 挡回（`trash-failed`）。改成**非破坏性**：覆盖写三件 + 断言形状，多余文件/兄弟包目录报错不清理。
- **hash 是"规范化后的 JObject"，不是裸字节**：给 `runtime.json` 尾部加一个换行**改不动任何 hash**
  （`HashCanonical` 先解析再序列化）。做变异检验必须改**语义**（值），改空白等于没改。
- **⚠️ `release_check.ps1` 侧：PS 5.1 的 `ConvertFrom-Json` 把对象键当大小写不敏感**。
  第一版新判据想数 `runtime.json` 的 `entries`，实跑得 **0**（期望 448）——不是路径错，是
  `ConvertFrom-Json` 直接抛：`无法转换 JSON 字符串，因为从该字符串转换的字典包含重复的键"Cow"和"cow"`。
  运行时关键词表里 `Cow` 与 `cow` 合法共存（JSON 允许），而 PS 5.1 走 `JavaScriptSerializer` ⇒ 必炸。
  ⇒ 每一条"恒 0 命中"都要当**探针失效**查（不能当"内容为空"）。治本＝PS 侧**不解析包 runtime/index**，
  结构校验交给 Newtonsoft 那两条路（`WorldbookPackageIntegrity` / runtime-smoke）；
  PS 侧只报字节数。**注意 `try/catch` 会把这个异常吞成 0**，比抛错更危险。

### 9. 主干侧顺带修的两处（`release_check.ps1`）
- 旧的三行遥测数的是 `rules/` 与 `personality_background/`（v1 目录，磁盘上早已不存在）
  ⇒ 永远 `WorldbookRuleFiles=0`，读起来像"内容丢了"。改为报 registry 包数 ＋ 包 runtime 字节数，
  并对二者加断言（≥1 包、字节数 >0）。
- `$required`（dist 必备文件清单）里**只有 registry `manifest.json`，没有包**——
  即 dist 可以只带一个指向不存在包的 registry 而发布门照过；运行时会在 `ReadAndVerify` 前
  抛 `WB2-MANIFEST-MISSING`。已加一段**按 registry 推导**的检查（不写死 slug）：
  包 manifest 在、是 v2、`entrypoints.runtime` / `index` 两文件都在，且源↔dist 三个文件 SHA256 相同。


