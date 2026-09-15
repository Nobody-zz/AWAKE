# 契约漂移修复 + tensionAxes 落地 + Q1 盲评改本地子智能体（2026-09-13）

> 三件事一次收口：①**补上编译门禁全红的根因**（契约 sha 漂移）；②**tensionAxes 按方案甲落进工具链**；③**Q1 脱名测试改用本地子智能体**复核（替掉能力不足的小模型）。
> 结论一句话：**编译门禁 0/76 → 76/76 全绿；三轴字段成为合法作者侧草稿；Q1 换强复核者后结果翻转——卡不是脸谱（命中率 74–77% vs 随机 8.3%），先前"认不出"是小模型的锅。**

---

## 一、编译门禁修复：契约 sha 漂移（76/76 转绿）

### 症状
`compile-verify` 全量 **76/76** 报 `persona.migration_required`；而 `docs/AUDIT-COMPILE-VERIFY-latest.json`（9-12 21:19）显示当时 **70/70 全绿**。

### 根因（两层，都是"改了契约没同步常量"）
`PersonaAuthoringContractAssets` 对四个内嵌契约资源做 sha256 硬校验，任一不符即 `IsValid=false`，全部卡判 `migration_required`。

| 契约资源 | 磁盘实际 sha256（前 16） | 代码常量（前 16） | |
|---|---|---|---|
| `awake.persona.authoring.v2.schema.json` | `2361F7F8E1575D74` | `5B5E704329C83838` | ✗ 漂移 |
| `persona-workbench-to-awake.crosswalk.v1.json` | `4906B794867D2304` | `D2F2AF9CBBCA3CAA` | ✗ 漂移 |
| `persona-canonical-json.v1.json` | `B9BE5523DAF6723E` | 同 | ✓ |
| `tag_registry.json` | `0E66E0344BA64CCF` | 同 | ✓ |

漂移成因（在途改动）：schema 删了 `foodPreference` 字段却**没更新 schema 常量**；crosswalk 被整篇重写（配合标签注册表扩容）却**常量只改了一半**。**排除行尾因素**（LF/CRLF 两种变体都对不上常量）。

### 第二层：crosswalk 取值缺口（雅那）
修完常量后仍剩 1 张：**雅那** `Crosswalk has no action for source value: facetStrengths.trait.pragmatic`。
- 它是 `facet_mapped` 编码的 3 个"映射型"标签之一（另两个：`behavior.bargains`、`expression.measured`），该编码 actions 只到 **4**；
- 而它的兄弟编码 `facet_preserve_only` 有 **5**；
- 全库 facet 取值分布：`{1:3, 2:59, 3:185, 4:79, 5:6}`——**5 是合法值**（另 5 处 5 都落在认 5 的编码上）。
- 另：所有编码的 `domain` 声明与 `actions` 键**普遍不一致**（axis 系列全部如此），说明 `domain` 是松声明、`actions` 才是真域，且 `domain` 不被 C# 读取。

### 改了什么（精确到行）
1. `PersonaAuthoringV2.cs` — `AuthoringSchemaSha256` / `CrosswalkSha256` 两个常量 → 实际值（一处 Edit，两行相邻）
2. `docs/persona-contract/...crosswalk.v1.json` — 给 `facet_mapped` 补 `"5": {"action":"preserve_only"}`（**字节级外科插入**，只动这一处；保持 CRLF；插入后 JSON 合法）
3. 其余三文件未动（`foodPreference` 删除是既有在途改动，我未触碰）

### 验证
```
5-compile  PASS  5.1s
ALL CARDS PASS compile verify   BUILD_PASS=76  BUILD_FAIL=0
```
关键卡复核：拉盖娅 / 蒙楚格 / 那得娅 / 雅那 **全部 buildOk=true**，`unknownRoot=[]`，`outOfRegistry*={}`。

### 遗留（已记录，未处理）
- **sha 常量 vs 行尾是定时炸弹**：契约 JSON 在盘上是 **CRLF**、`.cs` 是 **LF**。仓库开着 `autocrlf`，一旦 git 重新检出把行尾换了，sha 常量会再次失配。**当前修法只是"跟上现状"，没有消灭复发条件。** 建议后续改成"规范化行尾后再取摘要"（`digestMode: raw_utf8_bytes` 目前是裸字节）。
- **双尺子**：`PersonaWorkbench.Verify/Program.cs` 里另有一份 `allowedRoot` 白名单，与 `PersonaDocumentCodec.AllowedProperties` 内容重复（我这次两处都补了）。建议将来统一到一处。

---

## 二、tensionAxes 按方案甲落地

**背景**：v4 把三轴（E2）定为硬门，要求写进 `persona.json`；但工具链不认，照填必红——这就是"76 张里只有 2 张填三轴、且那 2 张一直编译不过"的原因。

**关键发现（省了一半工作量）**：**契约侧早已替甲案铺好**——
- `sourceVocabulary.rootFields` 里**已有** `"tensionAxes"`；
- crosswalk 里**已有**一条 `tensionAxes` 行：`sourceKind=root_field`，`targetField=migration.preservedLegacyData.tensionAxes`，`mappingKind=preserve_only`，`status=preserve_only`。

差的只是**工具链三处不认**。已补齐：

| # | 文件 | 改动 |
|---|---|---|
| 1 | `PersonaDocumentStorage.cs` | `AllowedProperties` += `"tensionAxes"`（否则 `persona.document_unknown_property`） |
| 2 | `PersonaCore.cs` | `PersonaDocument` += `public object? TensionAxes`（否则 `UnmappedMemberHandling.Disallow` 判 `persona.document_invalid`） |
| 3 | `PersonaAuthoringV2.cs` | `RootSourceIds` += `"tensionAxes"`（让契约闭合校验显式要求那条行） |
| 4 | `Verify/Program.cs` | `allowedRoot` += `"tensionAxes"`（保持镜像白名单同步） |

**语义定死**：`tensionAxes` = **作者侧草稿**，与 `facetStrengths` 同待遇——进 canonical 文档的 `migration.preservedLegacyData`、**物化时丢弃、不进运行时**。（Build 循环只处理 `tag/facet_strength/axis/legacy_field` 四类，`root_field` 直接跳过，所以它天然不会进运行时字段。）

自检：`PersonaDocument.TensionAxes` 用 `object?` 承载，序列化回写为原 JSON 对象，不破坏 Workbench 往返。

---

## 三、Q1 脱名测试：改用本地子智能体（结果翻转）

### 为什么要重做
上一轮我用本机小模型（qwen2.5）做复核，蒙楚格**未被认出**，当时判为"撞脸/无法区分"。但那次的限制写在报告里了：**小模型的盲评能力不足以支撑结论**。本轮按 Max 裁示，换**本地子智能体**（general-purpose subagent）复核。

### 协议（忠于 M6 定义："遮住姓名，仅凭口吻能否认出是谁"）
- 池子：12 张库赛特卡，各取最多 3 条 `selfClaimExamples`，共 **35 条**；
- 评审者只拿到：12 人的**中立第三人称身份卡**（姓名 + `identityFacts`，**刻意不用 `summary`/`publicDescription`**——那是"他怎么说话"的转述，与样本同源会造成循环自证）+ 打乱编号的匿名样本；
- 任务：把每条样本归给本人；对照基准 = 随机 **1/12 = 8.3%**。
- **两变体**：A 只遮人名；B **连部族名一起遮**（10 个：兀儿浑乃特/巴鲁吉特…等），逼辨认只能靠口吻。

### 结果

| 变体 | 遮蔽范围 | 命中 | 「无法判断」 |
|---|---|---|---|
| **A** | 只遮人名 | **27/35 = 77.1%** | 0 |
| **B** | 遮人名 + 部族名 | **26/35 = 74.3%** | 1 |

- 证据：`docs/evidence/Q1-SUBAGENT-{A,B}-20260913.json`、`Q1-COMPARE-20260913.json`

### 三条结论
1. **卡不是脸谱。** 严格遮蔽后命中率几乎不掉（77.1% → 74.3%），说明辨认**不是靠族属符号**，而是靠口吻、价值排序、在意的事。相对随机基线约 **9 倍**。
2. **上一轮的"认不出"是复核者的锅**，不是卡的锅——同一个池子，弱模型判"认不出"，强复核者判"认得出"。**"认证"这一层的可靠度完全取决于复核者强度**，这条对 M6 的可执行性很关键。
3. **真脸谱是局部的，且位置明确**：残留错误集中在两个**腔调簇**——
   - **"谨慎·信义·惜命·仁义"簇**：古速坎(P02) / 合努占(P03) / 拔该(P06) / 阿克鲁木(P10) 交叉误判（阿克鲁木仅 1/3）；
   - **"冷酷·权谋"簇**：呼鲁那格(P04) / 达思鲁儿(P09) 难分，雅那(P12) 有 1 条被判成呼鲁那格。
   
   **这两簇才是该动手的地方**，而不是全库重写。

### 附带修正
- 结合此前体检（91% 样本共用 `问号→破折号→引号` 结构框）：**样本格式高度同构，但内容仍可辨人**。可见 E5 的"结构框"是**格式病**，不等于**语气病**——两件事别混。
- 局限（诚实记账）：35 条里每人只有 2–3 条，且这 12 人是同一王国；跨王国是否会塌，本轮没测。

---

## 四、本轮文件清单

**改（均为 trae 在途的 `M` 文件，增量叠加、未推倒原有改动）**
- `docs/persona-contract/persona-workbench-to-awake.crosswalk.v1.json`（补 1 个动作）
- `tools/persona-workbench/src/PersonaWorkbench.Core/PersonaAuthoringV2.cs`（2 常量 + RootSourceIds）
- `tools/persona-workbench/src/PersonaWorkbench.Core/PersonaCore.cs`（TensionAxes 属性）
- `tools/persona-workbench/src/PersonaWorkbench.Core/PersonaDocumentStorage.cs`（白名单）
- `tools/persona-workbench/src/PersonaWorkbench.Verify/Program.cs`（镜像白名单）

**新增**
- `docs/AUDIT-COMPILE-VERIFY-latest.json`（刷新：76/76 PASS）
- `docs/evidence/Q1-SUBAGENT-{A,B}-20260913.json`、`docs/evidence/Q1-COMPARE-20260913.json`
- 本报告

**未动**：trae 那 203 项在途改动的其余部分；`foodPreference` 删除的既有改动（保留原样）。

**两个提交期的注意**
- `AWAKE/docs/evidence/` 在 `.gitignore` 第 48 行被忽略 ⇒ **本轮 Q1 证据是本地留档，不入库**。要长期可追溯，得另择跟踪路径（或改忽略规则，但 `.gitignore` 当前正被别人 staged，**不要动**）。
- git 索引里有**别的 agent staged 的 16 个文件**（`.gitignore` / `AGENTS.md` / 若干 csproj / `tag_registry.json` / `src/Prompts/NpcPromptTemplate.cs` …）。本次**未提交任何东西**；将来提交必须用 `git commit -- <精确路径>`，绝不能 `git add -A`。

---

## 五、2026-09-14 复核：遗留的行尾雷未拆，且比 §四记录的更坏

> **状态更新（2026-09-14 当晚）：已按 A 案修复，见 §5.6。** 以下 §5.1–5.5 保留复核时的原貌，未改写。

**状态：复核时仍未处理。** 09-13 只当"遗留"记了一句；09-14 复核后升级为**必须协调处置**的跨线项。以下为本机实读取证。

### 5.1 雷的机理（比原记录精确）

那四个契约摘要钉的**不是盘上某个路径，而是 assembly 内嵌资源**：
`tools/persona-workbench/src/PersonaWorkbench.Core/*.csproj:8-11` 用 `<EmbeddedResource Include="..\..\..\..\docs\persona-contract\awake.persona.authoring.v2.schema.json" LogicalName="PersonaWorkbench.Core.Contracts.awake.persona.authoring.v2.schema.json" />`（crosswalk / canonical-json 同法；`tag_registry` 来自 `ModuleData/Worldbook/persona_definitions/`）。
加载路径：`PersonaAuthoringV2.cs:543-564 LoadEmbedded()` → `GetManifestResourceStream` → `RequireDigest(schema, AuthoringSchemaSha256, ...)`（`:561-564`）。
⇒ **摘要 = 这些文件在"构建那一刻"的原始字节**。行尾一变，内嵌字节就变，`RequireDigest` 直接失败 → `IsValid=false` → 全部卡判 `migration_required`。

### 5.2 实际行尾是"混合"，两个环境各坏一半

`git ls-files --eol docs/persona-contract/`（2026-09-14 实读，共 18 件）：

| 文件 | 索引 | 工作树 |
|---|---|---|
| `awake.persona.authoring.v2.schema.json` | `i/lf` | **`w/crlf`** |
| `persona-workbench-to-awake.crosswalk.v1.json` | `i/lf` | **`w/crlf`** |
| `awake.persona.fixture-report.v1.schema.json` | `i/lf` | **`w/mixed`** |
| `persona-canonical-json.v1.json` | `i/lf` | `w/lf` |
| 其余 14 件 | `i/lf` | `w/lf` |

`core.autocrlf = true`，且**全仓（`D:\AWAKE-Dev` 与 `AWAKE\`）都没有 `.gitattributes`**。于是：

- **新鲜克隆 / 本机默认配置** → 检出时 LF→CRLF ⇒ 现在是 LF 的 `persona-canonical-json.v1.json` **会翻成 CRLF** ⇒ `CanonicalizationSha256` 失配。
- **`autocrlf=false` 或 Linux/macOS 检出** → 现在是 CRLF 的 schema、crosswalk **会翻成 LF** ⇒ `AuthoringSchemaSha256` / `CrosswalkSha256` 失配。

**两边各坏一半——这才是"定时"的确切含义。**（§四那句"LF/CRLF 两种变体都对不上常量"是当时那两件漂移的结论，与现在这条机制不同，别混。）

### 5.3 缩面：世界书侧**不受影响**

- `WorldbookPackageIntegrity.ComputePackageHash`（`src/WorldbookPackageIntegrity.cs:115-120`）吃的是**十六进制串**（`ParseHashBytes` 再拼接），`HashCanonical` / `Canonicalize` 走**解析后的 `JObject`** ⇒ 全部**与行尾无关**。
- 唯一读裸字节的是 `src/WorldbookRuntime.cs:160` 的 `manifest_sha256=` —— **只是日志**，不是断言。
⇒ 这颗雷**只在 persona 契约链上**，别扩大到世界书包完整性上去。

### 5.4 修法（二选一，**需 codex / trae 拍板后动**）

| | 做法 | 落点 | 代价 |
|---|---|---|---|
| **A（治本·推荐）** | `RequireDigest` 前先**规范化行尾**（CRLF/CR→LF）再取 sha，并把 4 个常量重钉到规范化后的值 | `PersonaAuthoringV2.cs:561-564` + 常量 `:502-505` | 改一次，之后行尾随便；但会**改变对外可见的契约 sha**（`docs/evidence/pwb-*.json` 里记着的旧值会对不上，需一并说明） |
| **B（治环境）** | 仓库根加 `.gitattributes`（`docs/persona-contract/*.json text eol=lf` 等），盘上文件统一到 LF、常量重钉 | 仓库根（**codex 地盘**）+ `PersonaAuthoringV2.cs` | 会把当前 CRLF 的 3 件改写（一次性 diff）；治住环境，但没治住"摘要取裸字节"这件事本身 |

**不建议**：只改本机 `core.autocrlf` —— 那是**本机私有配置**，保护不了新鲜克隆，等于没修。

### 5.5 归属（为什么本次没动）

- `tools/persona-workbench/src/PersonaWorkbench.Core/PersonaAuthoringV2.cs` 是 **trae 在途的 `M` 文件**（见 §四"本轮文件清单"）。
- `.gitattributes` 落在**仓库根**，属 codex 主干。
- 故 09-14 复核**只做取证与升级记录，未改一行**；修法 A/B 由相应线的负责人拍板。

### 5.6 修复记录（2026-09-14，按 A 案）

**前提变化**：trae / codex 已事实上退出日常维护 ⇒ §5.5 的归属顾虑解除，由世界书侧接手。

**改了什么**（`tools/persona-workbench/src/PersonaWorkbench.Core/PersonaAuthoringV2.cs`，1 文件 3 处）：

1. 新增 `StaticDigest(byte[])`：取 SHA-256 前先把行尾规范化为 LF（CRLF→LF、孤立 CR→LF）。
2. `RequireDigest`（原 `:618`）改调 `StaticDigest` —— 校验不再看裸字节。
3. 4 个 `Current*Sha256` 属性（原 `:533-536`）也改调 `StaticDigest` —— **校验值与上报值必须同源**，否则 `PersonaContractClosure.MatchesAssets`（`:295-297`，拿票据指纹比现算值）会自己跟自己不一致。
4. 重钉 **2 个常量**（另 2 个文件本来就是 LF，摘要不变，未动）：

| 常量 | 旧（裸字节 / CRLF） | 新（规范化 / LF） |
|---|---|---|
| `AuthoringSchemaSha256` | `2361F7F8E1575D74…` | `30647C7E583EC519…` |
| `CrosswalkSha256` | `4906B794867D2304…` | `C8A94B177DE10EE9…` |
| `CanonicalizationSha256` | `B9BE5523DAF6723E…` | 不变 |
| `RegistrySha256` | `0E66E0344BA64CCF…` | 不变 |

**验证（两条，均实跑）**：

1. **门禁 76/76**：重建 `PersonaWorkbench.Core`（重新内嵌当前磁盘上的 CRLF 契约）后跑 `PersonaWorkbench.Verify` → `TOTAL_CARDS=76 / BUILD_PASS=76 / BUILD_FAIL=0`。⇒ 规范化摘要与重钉常量吻合。
2. **行尾无关性**：同一份文件取 CRLF 与 LF 两种形态，规范化摘要**完全一致且等于新常量**——
   - schema：CRLF(8210B) 与 LF(7971B) → 同为 `30647C7E…`
   - crosswalk：CRLF(116699B) 与 LF(114655B) → 同为 `C8A94B17…`

   ⇒ "行尾一换就报 digest drifted" 这条**复发条件已根除**。

**没做**：`.gitattributes`（B 案）。A 案已让摘要与行尾无关，B 案剩下的价值只是让**工作树字节**确定，代价是改写全仓行尾、产生大面积 diff。留作可选洁癖项，不阻塞。

**旧证据的口径说明**：`docs/evidence/pwb-*.json` 里记的契约 sha 是**旧口径（裸字节）**的历史快照，与本次改动无关；**不改写历史**，仅在此注明口径已变。
